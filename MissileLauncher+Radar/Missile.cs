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
        public class Missile
        {
            private Program program;
            private int ID;
            private IMyProgrammableBlock missileComputer;


            public Missile(Program program, int ID)
            {
                this.program = program;
                this.ID = ID;
                missileComputer = (IMyProgrammableBlock)program.GridTerminalSystem.GetBlockWithName($"Missile Computer {ID}");
            }

            public void Launch(string broadcastTag)
            {
                missileComputer.TryRun("Launch " + "\"" + broadcastTag + "\"");
            }
        }
    }
}
