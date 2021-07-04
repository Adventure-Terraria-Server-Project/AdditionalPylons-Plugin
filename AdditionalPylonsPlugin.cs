using System;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Terraria.GameContent.NetModules;
using TShockAPI.Net;

namespace AdditionalPylons
{
  [ApiVersion(2, 1)]
  public class AdditionalPylonsPlugin : TerrariaPlugin
  {
    private const string permission_infiniteplace = "additionalpylons.inf";

    private readonly HashSet<int> pylonItemIDList = new HashSet<int>() { 4875, 4876, 4916, 4917, 4918, 4919, 4920, 4921, 4951 };
    private readonly HashSet<int> playersHoldingPylon = new HashSet<int>();

    #region Plugin Overrides
    public override void Initialize()
    {
      GetDataHandlers.PlayerUpdate.Register(OnPlayerUpdate);
      GetDataHandlers.PlaceTileEntity.Register(OnPlaceTileEntity, HandlerPriority.High);

      // STR Must be higher than TShock's to do some pre handling.
      GetDataHandlers.SendTileRect.Register(OnSendTileRect, HandlerPriority.High);
    }
    #endregion // Plugin overrides

    #region Plugin Hooks
    private void OnSendTileRect(object sender, GetDataHandlers.SendTileRectEventArgs e)
    {
      // Respect Highest priority plugin if they really needed it...
      if (this.isDisposed || e.Handled)
        return;

      // if player doesn't even have the permissions, no need to check data
      if (!e.Player.HasPermission(permission_infiniteplace))
        return;

      // Minimum sanity checks this STR is *probably* pylon
      if (e.Width != 3 || e.Length != 4)
        return;

      long savePosition = e.Data.Position;
      NetTile[,] tiles = new NetTile[e.Width, e.Length];

      for (int x = 0; x < e.Width; x++)
      {
        for (int y = 0; y < e.Length; y++)
        {
          tiles[x, y] = new NetTile(e.Data);
          if (tiles[x, y].Type != Terraria.ID.TileID.TeleportationPylon)
          {
            e.Data.Seek(savePosition, System.IO.SeekOrigin.Begin);
            return;
          }
        }
      }

      // Reset back the data
      e.Data.Seek(savePosition, System.IO.SeekOrigin.Begin);

      // Simply clear the Main system's pylon network to fool server >:DD
      // This works simply because the pylon system is refreshed anyways when it gets placed.
      // This section is required because TShock reimplmented STR with bouncer,
      // which then calls PlaceEntityNet which rejects the pylon because internally in Main.PylonSystem already contained a pylon of this type
      Main.PylonSystem._pylons.Clear();
    }

    private void OnPlayerUpdate(object sender, TShockAPI.GetDataHandlers.PlayerUpdateEventArgs e)
    {
      if (this.isDisposed || e.Handled)
        return;

      if (!e.Player.IsLoggedIn)
        return;

      if (!e.Player.HasPermission(permission_infiniteplace))
        return;

      int holdingItem = e.Player.TPlayer.inventory[e.SelectedItem].netID;
      bool alreadyHoldingPylon = playersHoldingPylon.Contains(e.PlayerId);
      bool isHoldingPylon = pylonItemIDList.Contains(holdingItem);

      if (alreadyHoldingPylon)
      {
        if (!isHoldingPylon)
        {
          // stopped holding pylon
          playersHoldingPylon.Remove(e.PlayerId);

          // Reload the Pylon system for player client
          SendPlayerPylonSystem(e.PlayerId, true);
        }
      }
      else
      {
        if (isHoldingPylon)
        {
          // Started holding pylon
          playersHoldingPylon.Add(e.PlayerId);

          // Clear Pylon System for player client
          SendPlayerPylonSystem(e.PlayerId, false);
        }
      }
    }

    private void OnPlaceTileEntity(object sender, TShockAPI.GetDataHandlers.PlaceTileEntityEventArgs e)
    {
      if (this.isDisposed || e.Handled)
        return;

      if (e.Type != 7)
        return;

      // Send STR to update non-inf pylons players's first pylon placement
      if (!e.Player.HasPermission(permission_infiniteplace))
      {
        TShockAPI.TSPlayer.All.SendTileRect((short)e.X, (short)e.Y, 3, 4);
        return;
      }

      Terraria.GameContent.Tile_Entities.TETeleportationPylon.Place(e.X, e.Y);

      // This is required to update the Server on the pylon list.
      // NOTE: Reset will broadcast changes to all players.
      Main.PylonSystem.Reset();

      // Send STR after manually doing TETeleportationPylon.Place() since other clients don't know about this pylon
      TShockAPI.TSPlayer.All.SendTileRect((short)e.X, (short)e.Y, 3, 4);

      playersHoldingPylon.Remove(e.Player.Index);

      //e.Handled = true;
    }
    #endregion // Plugin Hooks

    private void SendPlayerPylonSystem(int playerId, bool addPylons)
    {
      foreach (Terraria.GameContent.TeleportPylonInfo pylon in Main.PylonSystem.Pylons)
      {
        Terraria.Net.NetManager.Instance.SendToClient(
          NetTeleportPylonModule.SerializePylonWasAddedOrRemoved(pylon, addPylons ? NetTeleportPylonModule.SubPacketType.PylonWasAdded : NetTeleportPylonModule.SubPacketType.PylonWasRemoved),
          playerId
        );
      }
    }


    #region Plugin Properties
    public override string Name
    {
      get { return "AdditionalPylons"; }
    }
    public override Version Version
    {
      get { return System.Reflection.Assembly.GetAssembly(typeof(AdditionalPylonsPlugin)).GetName().Version; }
    }

    public override string Author
    {
      get { return "Stealownz"; }
    }
    public override string Description
    {
      get { return "You must construct additional pylons"; }
    }
    public override string UpdateURL
    {
      get { return "https://files.catbox.moe/2gsl9n.dll"; }
    }
    public AdditionalPylonsPlugin(Main game)
      : base(game)
    {
    }
    #endregion // Plugin Properties

    #region [IDisposable Implementation]
    private bool isDisposed = false;

    public bool IsDisposed
    {
      get { return this.isDisposed; }
    }

    protected override void Dispose(bool isDisposing)
    {
      if (this.IsDisposed)
        return;

      if (isDisposing)
      {
        GetDataHandlers.PlayerUpdate.UnRegister(OnPlayerUpdate);
        GetDataHandlers.PlaceTileEntity.UnRegister(OnPlaceTileEntity);
        GetDataHandlers.SendTileRect.UnRegister(OnSendTileRect);
      }

      base.Dispose(isDisposing);
      this.isDisposed = true;
    }
    #endregion // [IDisposable Implementation]
  }
}
