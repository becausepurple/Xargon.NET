using XargonNET.Objects;

namespace XargonNET.Ui;

/// <summary>The six purchases of buymenu() (xargon.c:923), by menu key.</summary>
public enum BuyItem { Health, Laser, RapidFire, Fireball, FiveFireballs, Invincible }

/// <summary>Outcome of one purchase attempt.</summary>
public enum BuyResult
{
    Bought,            // price deducted, effect applied
    NothingToDo,       // enough emeralds but already at the cap: no change, no charge
    NotEnoughEmeralds, // the caller shows dotextmsg(7)
    WrongForm,         // hero is transformed (bird/fish/...): silently ignored (B-E only)
}

/// <summary>Which laser palette (if any) the purchase asks the caller to set.</summary>
public enum BuyPalette { None, ExtraLaser, RapidFire }

/// <summary>SDL-free buy logic (the switch in buymenu(), xargon.c:931-992).</summary>
public static class BuyMenu
{
    public static BuyItem? ItemFor(char key) => key switch
    {
        'A' => BuyItem.Health, 'B' => BuyItem.Laser, 'C' => BuyItem.RapidFire,
        'D' => BuyItem.Fireball, 'E' => BuyItem.FiveFireballs, 'F' => BuyItem.Invincible,
        _ => null,
    };

    public static int Cost(BuyItem item) => item switch
    {
        BuyItem.Health => 15, BuyItem.Laser => 10, BuyItem.RapidFire => 25,
        BuyItem.Fireball => 5, BuyItem.FiveFireballs => 20, _ => 30,
    };

    /// <summary>
    /// One purchase, per the C. Check order: B-E first test the hero's form (silent no-op when transformed), then the
    /// emerald price (short => <see cref="BuyResult.NotEnoughEmeralds"/>), then the cap (at the cap: no change, no
    /// charge). A (health) and F (invincible) have no form check. The price is charged even if addinv ignores the add
    /// because the inventory is full (29), exactly as the C. The C's <c>while (invcount(inv_laser)&lt;5) addinv()</c>
    /// would loop forever on a full inventory; here it stops once an add has no effect.
    /// </summary>
    public static BuyResult Buy(BuyItem item, ObjectManager om, bool heroIsNormalPlayer, out BuyPalette palette)
    {
        palette = BuyPalette.None;
        var pl = om.Pl;
        if (item is >= BuyItem.Laser and <= BuyItem.FiveFireballs && !heroIsNormalPlayer) return BuyResult.WrongForm;
        int cost = Cost(item);
        if (pl.Emeralds < cost) return BuyResult.NotEnoughEmeralds;

        switch (item)
        {
            case BuyItem.Health:
                if (pl.Health >= 5) return BuyResult.NothingToDo;
                if (om.InvCount(Inv.Invin) == 0) pl.Ouched = -4;
                pl.Health++;
                break;
            case BuyItem.Laser:
                if (om.InvCount(Inv.Laser) >= 5) return BuyResult.NothingToDo;
                om.AddInv(Inv.Laser);
                palette = BuyPalette.ExtraLaser;
                break;
            case BuyItem.RapidFire:
                while (om.InvCount(Inv.Laser) < 5)
                {
                    int before = pl.NumInv;
                    om.AddInv(Inv.Laser);
                    if (pl.NumInv == before) break; // full inventory: the C would spin here
                }
                palette = BuyPalette.RapidFire;
                break;
            case BuyItem.Fireball:
                if (om.InvCount(Inv.Fire) >= 9) return BuyResult.NothingToDo;
                om.AddInv(Inv.Fire);
                break;
            case BuyItem.FiveFireballs:
                if (om.InvCount(Inv.Fire) >= 5) return BuyResult.NothingToDo;
                for (int i = 0; i < 5; i++) om.AddInv(Inv.Fire);
                break;
            default: // Invincible
                if (om.InvCount(Inv.Invin) != 0) return BuyResult.NothingToDo;
                om.AddInv(Inv.Invin);
                break;
        }
        pl.Emeralds -= cost; // never negative: guarded by the price check above
        return BuyResult.Bought;
    }

    /// <summary>One purchase including its side effects: the palette is set and a short purse shows dotextmsg(7).</summary>
    public static BuyResult Apply(BuyItem item, ObjectManager om, IPopupHost host)
    {
        var r = Buy(item, om, om.Obj(0).Kind == ObjectKind.Player, out var pal);
        if (pal == BuyPalette.ExtraLaser) Effects.SetExtraLaserPalette(om.Screen);
        else if (pal == BuyPalette.RapidFire) Effects.SetRapidFirePalette(om.Screen);
        if (r == BuyResult.NotEnoughEmeralds) host.TextMessage(7, PopupStyle.Plain);
        return r;
    }
}
