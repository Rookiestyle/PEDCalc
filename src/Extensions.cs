using System;
using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using KeePassLib.Utility;
using PluginTools;

namespace PEDCalc
{
  internal class DataMigration
  {
    [Flags]
    private enum Migrations
    { //Update CheckAndMigrate(PwDatabase db) if changes are done here
      None = 0,
      Entry2CustomData = 1,
    }

    public static bool CheckAndMigrate(PwDatabase db)
    {
      //Do NOT create a 'ALL' flag as this will be stored as 'ALL' and by that, no additional migrations would be done
      Migrations m = Migrations.None;
      foreach (var v in Enum.GetValues(typeof(Migrations))) m |= (Migrations)v;
      return CheckAndMigrate(db, m);
    }

    /// <summary>
    /// Perform all kind of migrations between different KeePassOTP versions
    /// </summary>
    /// <param name="db"></param>
    /// <returns>true if something was migrated, false if nothing was done</returns>
    private static bool CheckAndMigrate(PwDatabase db, Migrations omFlags)
    {
      string sMigration = "PEDCalc.MigrationStatus";
      bool bMigrated = false;

      Migrations mStatusOld;
      try { mStatusOld = (Migrations)Enum.Parse(typeof(Migrations), db.CustomData.Get(sMigration), true); }
      catch { mStatusOld = Migrations.None; }
      Migrations mStatusNew = mStatusOld;

      if (MigrationRequired(Migrations.Entry2CustomData, omFlags, mStatusOld))
      {
        bMigrated |= MigrateEntry2CustomData(db) > 0;
        mStatusNew |= Migrations.Entry2CustomData;
      }

      if ((mStatusNew != mStatusOld) || bMigrated)
      {
        db.CustomData.Set(sMigration, mStatusNew.ToString());
        db.SettingsChanged = DateTime.UtcNow;
        db.Modified = true;
        KeePass.Program.MainForm.UpdateUI(false, null, false, null, false, null, KeePass.Program.MainForm.ActiveDatabase == db);
      }
      return bMigrated;
    }

    private static int MigrateEntry2CustomData(PwDatabase db)
    {
      string sDaysField = "PEDCalc.days"; //was used in previous versions which supported days only
      int i = 0;
      var lEntries = db.RootGroup.GetEntries(true);
      foreach (PwEntry pe in lEntries)
      {
        string s = pe.Strings.ReadSafe(Configuration.Interval);
        if (string.IsNullOrEmpty(s)) s = pe.Strings.ReadSafe(sDaysField);

        if (string.IsNullOrEmpty(s)) continue;

        pe.CustomData.Set(Configuration.Interval, s);

        pe.Strings.Remove(Configuration.Interval);
        pe.Strings.Remove(sDaysField);

        i++;
      }

      var lGroups = db.RootGroup.GetFlatGroupList();
      lGroups.AddFirst(db.RootGroup);
      foreach (PwGroup pg in lGroups)
      {
        string s = pg.CustomData.Get(sDaysField);
        if (!string.IsNullOrEmpty(s))
        {
          pg.CustomData.Set(Configuration.Interval, s);
          pg.CustomData.Remove(sDaysField);
          i++;
        }
      }
      return i;
    }

    private static bool MigrationRequired(Migrations mMigrate, Migrations mFlags, Migrations status)
    {
      if ((mMigrate & mFlags) != mMigrate) return false; //not requested
      if ((mMigrate & status) == mMigrate) return false; //already done
      return true;
    }
  }
  internal static class EntryExtensions
  {
    internal static void RecalcExpiry(this PwEntry pe)
    {
      pe.RecalcExpiry(false);
    }

    internal static void RecalcExpiry(this PwEntry pe, bool forceRecalculation)
    {
      if (!pe.Expires)
        return; //password does not expire: nothing to do

      if (!forceRecalculation && !RecalcRequired(pe)) return;

      PEDCalcValue days = GetPEDValue(pe, true);
      PluginDebug.AddInfo("Recalc expiry date", pe.Uuid.ToString(), "Force: " + forceRecalculation.ToString(), "Old: " + pe.ExpiryTime.ToString("YYYYMMddTHHmmssZ"),
        "New: " + days.NewExpiryDateUtc.ToString("yyyyMMddTHHmmssZ"),
        "Recalc required: " + (days.Specific && (pe.ExpiryTime != days.NewExpiryDateUtc)).ToString());
      if (!days.Specific)
        return;

      if (days.NewExpiryDateUtc == pe.ExpiryTime) return;
      pe.ExpiryTime = days.NewExpiryDateUtc;
      pe.Touch(true, false);
      Tools.RefreshEntriesList(true);
    }

    internal static bool RecalcRequired(this PwEntry pe)
    {
      if (pe.History.UCount < 1)
        return false; // no history exists, this is a new entry and not a password change: nothing to do

      byte[] pw_now = pe.Strings.GetSafe(PwDefs.PasswordField).ReadUtf8();
      byte[] pw_prev = pe.History.GetAt(pe.History.UCount - 1).Strings.GetSafe(PwDefs.PasswordField).ReadUtf8();

      string days_string = ReadPEDCString(pe);
      string days_string_prev = ReadPEDCString(pe.History.GetAt(pe.History.UCount - 1));

      DateTime expiry_now = pe.ExpiryTime;
      DateTime expiry_prev = pe.History.GetAt(pe.History.UCount - 1).ExpiryTime;

      //check whether recalculation is required
      bool bNoRecalc = ((expiry_now != expiry_prev)//expiry time has been changed manually
        || ((days_string == days_string_prev)//	PEDCalc password lifetime value is the same as before
        && MemUtil.ArraysEqual(pw_now, pw_prev))); //	Passwords are the same
      if (bNoRecalc)
        PluginDebug.AddInfo("Recalc expiry date", pe.Uuid.ToString(), "Recalc not required");
      else
        PluginDebug.AddInfo("Recalc expiry date", pe.Uuid.ToString(), "Recalc required");
      return !bNoRecalc;
    }

    internal static void Expire(this PwEntry pe, bool bExpireAll)
    {
      if (!pe.Expires && !bExpireAll) return;
      if (pe.Expires && (pe.ExpiryTime < DateTime.UtcNow)) return;

      Configuration.SkipRecalc = true;
      PluginDebug.AddInfo("Expire entry", pe.Uuid.ToString());
      pe.CreateBackup(KeePass.Program.MainForm.ActiveDatabase);
      pe.Expires = true;
      pe.ExpiryTime = PEDCalcValue.UnixStart;
      pe.Touch(true, false);
    }

    internal static PEDCalcValue GetPEDValue(this PwEntry pe, bool recursion)
    {
      return PEDCValueDAO.GetPEDCValue(pe, recursion);
    }

    internal static PEDCalcValue GetPEDValueInherit(this PwEntry pe)
    {
      return pe.ParentGroup.GetPEDValue(true);
    }

    internal static string ReadPEDCString(this PwEntry pe)
    {
      return pe.CustomData.Get(Configuration.Interval);
    }

    internal static void SavePEDCString(this PwEntry pe, PEDCalcValue days)
    {
      if (days.Inherit)
        pe.CustomData.Remove(Configuration.Interval);
      else
        pe.CustomData.Set(Configuration.Interval, days.ToString());
      PEDCValueDAO.Invalidate(pe);
    }

    internal static PEDCalcValue ReadPEDCString(this ProtectedStringDictionary psd)
    {
      return null;
      /*
			string sPEDC = psd.ReadSafe(Configuration.DaysField);
			if (string.IsNullOrEmpty(sPEDC))
				sPEDC = psd.ReadSafe(Configuration.Interval);
			if (!string.IsNullOrEmpty(sPEDC))
				return PEDCalcValue.ConvertFromString(sPEDC);
			return null;
			*/
    }
  }

  internal static class GroupExtensions
  {
    internal static void RecalcExpiry(this PwGroup pg)
    {
      PwObjectList<PwEntry> entries = pg.GetEntries(false);
      foreach (PwEntry pe in entries)
      {
        if (pe.GetPEDValue(false).Inherit)
          pe.RecalcExpiry(true);
      }

      PwObjectList<PwGroup> groups = pg.GetGroups(false);
      foreach (PwGroup g in groups)
      {
        if (g.GetPEDValue(false).Inherit)
          RecalcExpiry(g);
      }
    }

    internal static void Expire(this PwGroup pg, bool bExpireAll)
    {
      PwObjectList<PwEntry> entries = pg.GetEntries(false);
      foreach (PwEntry pe in entries)
        pe.Expire(bExpireAll);

      PwObjectList<PwGroup> groups = pg.GetGroups(false);
      foreach (PwGroup g in groups)
        g.Expire(bExpireAll);
    }

    internal static PEDCalcValue GetPEDValue(this PwGroup pg, bool recursion)
    {
      string days_string = pg.ReadPEDCString();
      PEDCalcValue PEDValue = PEDCalcValue.ConvertFromString(days_string);

      if (PEDValue.Inherit && recursion && pg.ParentGroup != null)
        return GetPEDValue(pg.ParentGroup, true);
      if (PEDValue.Inherit && pg.ParentGroup == null)
        PEDValue.unit = PEDC.Off;
      return PEDValue;
    }

    internal static PEDCalcValue GetPEDValueInherit(this PwGroup pg)
    {
      if (pg.ParentGroup == null)
        return new PEDCalcValue(PEDC.Off);
      return pg.ParentGroup.GetPEDValue(true);
    }

    internal static string ReadPEDCString(this PwGroup pg)
    {
      return pg.CustomData.Get(Configuration.Interval);
    }

    internal static void SavePEDCString(this PwGroup pg, PEDCalcValue days)
    {
      if (days.Inherit)
        pg.CustomData.Remove(Configuration.Interval);
      else
        pg.CustomData.Set(Configuration.Interval, days.ToString());
      PEDCValueDAO.Invalidate(pg);
    }
  }
}
