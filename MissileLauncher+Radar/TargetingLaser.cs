using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class TargetingLaser
        {
            private Program program;
            private int ID;

            private string broadcastTag;

            private IMyMotorStator azimuthRotor;
            private IMyMotorStator elevationRotor;
            private IMyShipController laserController;
            private List<IMyCameraBlock> cameraArray = new List<IMyCameraBlock>();
            private List<Vector3> cameraPositionsFromRotationLocal = new List<Vector3>();

            private Vector3 rotationPointLocal;
            private Matrix referenceMatrix;

            private int raycastCounter;
            private float maxRaycastDistance;
            private float raycastDistanceGrowthSpeed;
            private float sensitivity;

            private float timeSinceLastRaycast;
            private float timeSinceLastDetection;
            private float timeSinceLastUniqueDetection;
            private float timeSinceLastTargetDetection;
            private int raycastsSinceLastTargetDetection;

            private int matchingDetectionCounter;

            private MyDetectedEntityInfo detectedTarget;
            private MyDetectedEntityInfo previouslyDetectedTarget;
            private MyDetectedEntityInfo lockedTarget;

            private Vector3 outputTargetPosition;
            private Vector3 outputTargetVelocity;

            private Matrix launcherInfo;

            private MyDetectedEntityInfo emptyTarget = new MyDetectedEntityInfo();

            private Vector3 aimingVectorLocal;

            private float azimuthError;
            private float elevationError;
            private PIDControl azimuthPID;
            private PIDControl elevationPID;

            private int elevationRotorSignCorrection;

            private bool manualOverride = false;

            public TargetingLaser(Program program, int ID, string broadcastTag, float sensitivity = 0.05f, float maxRaycastDistance = 5000, float raycastDistanceGrowthSpeed = 200)
            {
                this.program = program;
                this.ID = ID;
                this.broadcastTag = broadcastTag;
                this.sensitivity = sensitivity;
                this.maxRaycastDistance = maxRaycastDistance;
                this.raycastDistanceGrowthSpeed = raycastDistanceGrowthSpeed;
                

                azimuthRotor = (IMyMotorStator)program.GridTerminalSystem.GetBlockWithName($"Azimuth Rotor {ID}");
                elevationRotor = (IMyMotorStator)program.GridTerminalSystem.GetBlockWithName($"Elevation Rotor {ID}");
                laserController = (IMyShipController)program.GridTerminalSystem.GetBlockWithName($"Laser Controller {ID}");
                program.GridTerminalSystem.GetBlockGroupWithName($"Camera Array {ID}").GetBlocksOfType<IMyCameraBlock>(cameraArray);

                rotationPointLocal = new Vector3(0, Vector3.TransformNormal(elevationRotor.GetPosition() - azimuthRotor.GetPosition(), Matrix.Transpose(azimuthRotor.WorldMatrix)).Y, 0);

                referenceMatrix = azimuthRotor.WorldMatrix;

                Quaternion azimuthRotation = Quaternion.CreateFromAxisAngle(referenceMatrix.Up, -azimuthRotor.Angle);
                Quaternion elevationRotation = Quaternion.CreateFromAxisAngle(referenceMatrix.Right, -elevationRotor.Angle);
                Quaternion totalRotation = Quaternion.Concatenate(elevationRotation, azimuthRotation);

                Matrix.Transform(ref referenceMatrix, ref totalRotation, out referenceMatrix);

                referenceMatrix.Translation = Vector3.Transform(rotationPointLocal, azimuthRotor.WorldMatrix);

                foreach (IMyCameraBlock camera in cameraArray)
                {
                    camera.EnableRaycast = true;
                    Vector3 positionFromRotationLocal = Vector3.TransformNormal(camera.GetPosition() - referenceMatrix.Translation, Matrix.Transpose(referenceMatrix));
                    cameraPositionsFromRotationLocal.Add(positionFromRotationLocal);
                }

                azimuthPID = new PIDControl(25, 2, 0.1f);
                elevationPID = new PIDControl(25, 2, 0.1f);

                elevationRotorSignCorrection = -(int)Math.Round(Vector3.Dot(elevationRotor.WorldMatrix.Up, referenceMatrix.Right));
            }

            public void Run()
            {
                float timeDeltaSeconds = (float)program.Runtime.TimeSinceLastRun.TotalSeconds;
                float timeDeltaMiliseconds = (float)program.Runtime.TimeSinceLastRun.TotalMilliseconds;
                timeSinceLastRaycast += timeDeltaMiliseconds;
                timeSinceLastDetection += timeDeltaSeconds;
                timeSinceLastUniqueDetection += timeDeltaSeconds;

                if (!lockedTarget.IsEmpty())
                {
                    timeSinceLastTargetDetection += timeDeltaSeconds;
                }

                referenceMatrix = azimuthRotor.WorldMatrix;

                Quaternion azimuthRotation = Quaternion.CreateFromAxisAngle(referenceMatrix.Up, -azimuthRotor.Angle);
                Quaternion elevationRotation = Quaternion.CreateFromAxisAngle(referenceMatrix.Right, -elevationRotor.Angle);
                Quaternion totalRotation = azimuthRotation * elevationRotation;

                Matrix.Transform(ref referenceMatrix, ref totalRotation, out referenceMatrix);

                referenceMatrix.Translation = Vector3.Transform(rotationPointLocal, azimuthRotor.WorldMatrix);


                if ((laserController.MoveIndicator.Y == -1 || (timeSinceLastTargetDetection > 5 && raycastsSinceLastTargetDetection >= 5)) && !lockedTarget.IsEmpty())
                {
                    lockedTarget = emptyTarget;
                    timeSinceLastTargetDetection = 0;
                    matchingDetectionCounter = 0;
                    raycastsSinceLastTargetDetection = 0;
                }

                Vector3 raycastDirectionLocal;
                float raycastDistance;

                if (Math.Abs(azimuthError) < 5 * Math.PI / 180 && Math.Abs(elevationError) < 5 * Math.PI / 180 && !lockedTarget.IsEmpty() && manualOverride == false)
                {
                    Vector3 raycastVectorLocal = aimingVectorLocal - cameraPositionsFromRotationLocal[raycastCounter % cameraArray.Count];
                    raycastDirectionLocal = Vector3.Normalize(raycastVectorLocal);
                    raycastDistance = (float)raycastVectorLocal.Length();
                }
                else
                {
                    Vector3 raycastVectorLocal = -Vector3.UnitZ * maxRaycastDistance - cameraPositionsFromRotationLocal[raycastCounter % cameraArray.Count];
                    raycastDirectionLocal = Vector3.Normalize(raycastVectorLocal);
                    raycastDistance = (float)raycastVectorLocal.Length();
                }

                float raycastTimeDelta = raycastDistance / (2 * cameraArray.Count);

                if (cameraArray[raycastCounter % cameraArray.Count].TimeUntilScan(raycastDistance) == 0 && timeSinceLastRaycast >= raycastTimeDelta && ((!lockedTarget.IsEmpty() && manualOverride == false) || laserController.MoveIndicator.Y == 1))
                {
                    MyDetectedEntityInfo raycastResult = cameraArray[raycastCounter % cameraArray.Count].Raycast(raycastDistance, raycastDirectionLocal);
                    timeSinceLastRaycast = 0;
                    raycastCounter += 1;

                    if (!lockedTarget.IsEmpty() && raycastResult.EntityId != lockedTarget.EntityId)
                    {
                        raycastsSinceLastTargetDetection += 1;
                    }
                    if (!raycastResult.IsEmpty())
                    {
                        detectedTarget = raycastResult;
                        timeSinceLastDetection = 0;

                        if (lockedTarget.IsEmpty())
                        {
                            if (detectedTarget.EntityId == previouslyDetectedTarget.EntityId)
                            {
                                matchingDetectionCounter += 1;
                            }
                            else
                            {
                                timeSinceLastUniqueDetection = 0;
                                matchingDetectionCounter = 0;
                            }

                            previouslyDetectedTarget = detectedTarget;

                            if (timeSinceLastUniqueDetection > 2 && matchingDetectionCounter >= 3)
                            {
                                lockedTarget = detectedTarget;
                            }
                        }

                        else if (detectedTarget.EntityId == lockedTarget.EntityId)
                        {
                            lockedTarget = detectedTarget;
                            timeSinceLastTargetDetection = 0;
                            raycastsSinceLastTargetDetection = 0;
                        }
                    }
                }


                if (!lockedTarget.IsEmpty())
                {
                    float raycastDistanceGrowth = raycastDistanceGrowthSpeed * timeSinceLastTargetDetection;
                    Vector3 estimatedTargetPositionWorld = lockedTarget.Position + (Vector3)lockedTarget.Velocity * timeSinceLastTargetDetection;
                    Vector3 estimatedTargetPositionLocal = Vector3.TransformNormal(estimatedTargetPositionWorld - referenceMatrix.Translation, Matrix.Transpose(referenceMatrix));
                    Vector3 estimatedTargetDirectionLocal = Vector3.Normalize(estimatedTargetPositionLocal);

                    outputTargetPosition = estimatedTargetPositionWorld;
                    outputTargetVelocity = lockedTarget.Velocity;

                    launcherInfo = laserController.CubeGrid.WorldMatrix;

                    MyTuple<Vector3, Vector3, Matrix> targetInfo = new MyTuple<Vector3, Vector3, Matrix>(outputTargetPosition, outputTargetVelocity, launcherInfo);

                    program.IGC.SendBroadcastMessage(broadcastTag, targetInfo);

                    if (manualOverride == false)
                    {
                        aimingVectorLocal = estimatedTargetDirectionLocal * (estimatedTargetPositionLocal.Length() + raycastDistanceGrowth);
                        aimingVectorLocal = aimingVectorLocal.Length() > maxRaycastDistance ? Vector3.Normalize(aimingVectorLocal) * maxRaycastDistance : aimingVectorLocal;

                        float localTargetRange = (float)aimingVectorLocal.Length();
                        azimuthError = (float)Math.Atan2(aimingVectorLocal.X, -aimingVectorLocal.Z);
                        elevationError = (float)Math.Asin(aimingVectorLocal.Y / localTargetRange);

                        azimuthRotor.TargetVelocityRad = azimuthPID.Run(azimuthError, timeDeltaSeconds);
                        elevationRotor.TargetVelocityRad = elevationRotorSignCorrection * elevationPID.Run(elevationError, timeDeltaSeconds);
                    }
                    else if (manualOverride == true)
                    {
                        aimingVectorLocal = -Vector3.UnitZ * estimatedTargetPositionLocal.Length() + raycastDistanceGrowth;
                        aimingVectorLocal = aimingVectorLocal.Length() > maxRaycastDistance ? Vector3.Normalize(aimingVectorLocal) * maxRaycastDistance : aimingVectorLocal;

                        elevationRotor.TargetVelocityRad = laserController.RotationIndicator.X * sensitivity;
                        azimuthRotor.TargetVelocityRad = laserController.RotationIndicator.Y * sensitivity;
                    }
                }

                else
                {
                    elevationRotor.TargetVelocityRad = laserController.RotationIndicator.X * sensitivity;
                    azimuthRotor.TargetVelocityRad = laserController.RotationIndicator.Y * sensitivity;
                }
            }


        }
    }
}
