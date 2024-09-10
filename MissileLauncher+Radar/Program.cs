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
    partial class Program : MyGridProgram
    {
        MissileLauncher missileLauncher;
        TargetingLaser laser1;
        Dictionary<string, Action<int>> commands = new Dictionary<string, Action<int>>();
        MyCommandLine commandLine = new MyCommandLine();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            missileLauncher = new MissileLauncher(this, 1);
            laser1 = new TargetingLaser(this, 0, $"Targeting Laser Data {0}");

            commands["Launch"] = missileLauncher.LaunchNextAvailableMissile;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            laser1.Run();
            if (commandLine.TryParse(argument))
            {
                string commandName = commandLine.Argument(0);
                string commandArgument = commandLine.Argument(1);
                Action<int> command;

                if (commandName != null && commandArgument != null)
                {
                    if (commands.TryGetValue(commandName, out command))
                    {
                        command(int.Parse(commandArgument));
                    }
                }
            }
        }
    }
}
