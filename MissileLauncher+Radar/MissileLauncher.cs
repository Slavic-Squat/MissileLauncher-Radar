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
        public class MissileLauncher
        {
            private Queue<Missile> missileQueue = new Queue<Missile>();

            public MissileLauncher(Program program, int numberOfMissiles)
            {
                for (int i = 0; i <= numberOfMissiles - 1; i++)
                {
                    missileQueue.Enqueue(new Missile(program, i));
                }
            }

            public void LaunchNextAvailableMissile(int targetingLaserNumber)
            {
                if (missileQueue.Count != 0)
                {
                    missileQueue.Dequeue().Launch($"Targeting Laser Data {targetingLaserNumber}");
                }
            }


        }
    }
}
