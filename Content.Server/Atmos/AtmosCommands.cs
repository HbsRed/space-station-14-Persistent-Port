﻿#nullable enable
using System;
using Content.Server.GameObjects.Components.Atmos;
using Content.Shared.Atmos;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.Atmos
{
    public class AddAtmos : IClientCommand
    {
        public string Command => "addatmos";
        public string Description => "Adds atmos support to a grid.";
        public string Help => "addatmos <GridId>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            if (args.Length < 1) return;
            if(!int.TryParse(args[0], out var id)) return;

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid already has an atmosphere.");
                return;
            }

            grid.AddComponent<GridAtmosphereComponent>();

            shell.SendText(player, $"Added atmosphere to grid {id}.");
        }
    }

    public class ListGases : IClientCommand
    {
        public string Command => "listgases";
        public string Description => "Prints a list of gases and their indices.";
        public string Help => "listgases";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            foreach (var gasPrototype in Atmospherics.Gases)
            {
                shell.SendText(player, $"{gasPrototype.Name} ID: {gasPrototype.ID}");
            }
        }
    }

    public class AddGas : IClientCommand
    {
        public string Command => "addgas";
        public string Description => "Adds gas at a certain position.";
        public string Help => "addgas <X> <Y> <GridId> <Gas> <moles>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            var gasId = -1;
            var gas = (Gas) (-1);
            if (args.Length < 5) return;
            if(!int.TryParse(args[0], out var x)
               || !int.TryParse(args[1], out var y)
               || !int.TryParse(args[2], out var id)
               || !(int.TryParse(args[3], out gasId) || Enum.TryParse(args[3], true, out gas))
               || !float.TryParse(args[4], out var moles)) return;

            if (gas != (Gas) (-1))
            {
                gasId = (int)gas;
            }

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();
            var indices = new MapIndices(x, y);
            var tile = gam.GetTile(indices);

            if (tile == null)
            {
                shell.SendText(player, "Invalid coordinates.");
                return;
            }

            if (tile.Air == null)
            {
                shell.SendText(player, "Can't add gas to that tile.");
                return;
            }

            if (gasId != -1)
            {
                tile.Air.AdjustMoles(gasId, moles);
                gam.Invalidate(indices);
                return;
            }

            tile.Air.AdjustMoles(gas, moles);
            gam.Invalidate(indices);
        }
    }

    public class FillGas : IClientCommand
    {
        public string Command => "fillgas";
        public string Description => "Adds gas to all tiles in a grid.";
        public string Help => "fillgas <GridId> <Gas> <moles>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            var gasId = -1;
            var gas = (Gas) (-1);
            if (args.Length < 3) return;
            if(!int.TryParse(args[0], out var id)
               || !(int.TryParse(args[1], out gasId) || Enum.TryParse(args[1], true, out gas))
               || !float.TryParse(args[2], out var moles)) return;

            if (gas != (Gas) (-1))
                gasId = (int)gas;

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();

            foreach (var tile in gam)
            {
                if (gasId != -1)
                {
                    tile.Air?.AdjustMoles(gasId, moles);
                    gam.Invalidate(tile.GridIndices);
                    continue;
                }

                tile.Air?.AdjustMoles(gas, moles);
                gam.Invalidate(tile.GridIndices);
            }
        }
    }

    public class RemoveGas : IClientCommand
    {
        public string Command => "removegas";
        public string Description => "Removes an amount of gases.";
        public string Help => "removegas <X> <Y> <GridId> <amount> <ratio>\nIf <ratio> is true, amount will be treated as the ratio of gas to be removed.";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            if (args.Length < 5) return;
            if(!int.TryParse(args[0], out var x)
               || !int.TryParse(args[1], out var y)
               || !int.TryParse(args[2], out var id)
               || !float.TryParse(args[3], out var amount)
               || !bool.TryParse(args[4], out var ratio)) return;

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();
            var indices = new MapIndices(x, y);
            var tile = gam.GetTile(indices);

            if (tile == null)
            {
                shell.SendText(player, "Invalid coordinates.");
                return;
            }

            if (tile.Air == null)
            {
                shell.SendText(player, "Can't remove gas from that tile.");
                return;
            }

            if (ratio)
                tile.Air.RemoveRatio(amount);
            else
                tile.Air.Remove(amount);

            gam.Invalidate(indices);
        }
    }

    public class ClearAtmos : IClientCommand
    {
        public string Command => "clearatmos";
        public string Description => "Clear a grid of all gases.";
        public string Help => "clearatmos <GridId>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            if (args.Length < 1) return;
            if (!int.TryParse(args[0], out var id))
            {
                shell.SendText(player, "Not enough arguments!");
            }

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();

            var tiles = 0;
            var moles = 0f;
            foreach (var tile in gam)
            {
                if (tile.Air == null || tile.Air.Immutable) continue;
                tiles++;
                moles += tile.Air.TotalMoles;
                tile.Air.RemoveRatio(1f);
                gam.Invalidate(tile.GridIndices);
            }

            shell.SendText(player, $"Removed {moles} moles from {tiles} tiles.");
        }
    }

    public class SetTemperature : IClientCommand
    {
        public string Command => "settemp";
        public string Description => "Sets a tile's temperature (in kelvin).";
        public string Help => "Usage: settemp <X> <Y> <GridId> <Temperature>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            if (args.Length < 4) return;
            if(!int.TryParse(args[0], out var x)
               || !int.TryParse(args[1], out var y)
               || !int.TryParse(args[2], out var id)
               || !float.TryParse(args[3], out var temperature)) return;

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (temperature < Atmospherics.TCMB)
            {
                shell.SendText(player, "Invalid temperature.");
                return;
            }

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();
            var indices = new MapIndices(x, y);
            var tile = gam.GetTile(indices);

            if (tile == null)
            {
                shell.SendText(player, "Invalid coordinates.");
                return;
            }

            if (tile.Air == null)
            {
                shell.SendText(player, "Can't change that tile's temperature.");
                return;
            }

            tile.Air.Temperature = temperature;
            gam.Invalidate(indices);
        }
    }

    public class SetAtmosTemperature : IClientCommand
    {
        public string Command => "setatmostemp";
        public string Description => "Sets a grid's temperature (in kelvin).";
        public string Help => "Usage: setatmostemp <GridId> <Temperature>";
        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            if (args.Length < 2) return;
            if(!int.TryParse(args[0], out var id)
               || !float.TryParse(args[1], out var temperature)) return;

            var gridId = new GridId(id);

            var mapMan = IoCManager.Resolve<IMapManager>();

            if (temperature < Atmospherics.TCMB)
            {
                shell.SendText(player, "Invalid temperature.");
                return;
            }

            if (!gridId.IsValid() || !mapMan.TryGetGrid(gridId, out var gridComp))
            {
                shell.SendText(player, "Invalid grid ID.");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();

            if (!entMan.TryGetEntity(gridComp.GridEntityId, out var grid))
            {
                shell.SendText(player, "Failed to get grid entity.");
                return;
            }

            if (!grid.HasComponent<GridAtmosphereComponent>())
            {
                shell.SendText(player, "Grid doesn't have an atmosphere.");
                return;
            }

            var gam = grid.GetComponent<GridAtmosphereComponent>();

            var tiles = 0;
            foreach (var tile in gam)
            {
                if (tile.Air == null)
                    continue;

                tiles++;

                tile.Air.Temperature = temperature;
                gam.Invalidate(tile.GridIndices);
            }

            shell.SendText(player, $"Changed the temperature of {tiles} tiles.");
        }
    }
}
