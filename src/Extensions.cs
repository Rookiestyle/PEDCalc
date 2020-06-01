using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using KeePassLib.Utility;
using PluginTools;
using System;

namespace PEDCalc
{
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
			KeePass.Program.MainForm.UpdateUI(false, null, false, null, true, null, true);
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

		internal static void Expire(this PwEntry pe)
		{
			if (!pe.Expires) return;
			if (pe.ExpiryTime < DateTime.UtcNow) return;

			Configuration.SkipRecalc = true;
			PluginDebug.AddInfo("Expire entry", pe.Uuid.ToString());
			pe.CreateBackup(KeePass.Program.MainForm.ActiveDatabase);
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
			if (pe.Strings.Exists(Configuration.Interval))
				return pe.Strings.ReadSafe(Configuration.Interval);
			return pe.Strings.ReadSafe(Configuration.DaysField);
		}

		internal static void SavePEDCString(this PwEntry pe, PEDCalcValue days)
		{
			pe.Strings.Remove(Configuration.DaysField);
			if (days.Inherit)
				pe.Strings.Remove(Configuration.Interval);
			else
				pe.Strings.Set(Configuration.Interval, new ProtectedString(false, days.ToString()));
			PEDCValueDAO.Invalidate(pe);
		}

		internal static PEDCalcValue ReadPEDCString(this ProtectedStringDictionary psd)
		{
			string sPEDC = psd.ReadSafe(Configuration.DaysField);
			if (string.IsNullOrEmpty(sPEDC))
				sPEDC = psd.ReadSafe(Configuration.Interval);
			if (!string.IsNullOrEmpty(sPEDC))
				return PEDCalcValue.ConvertFromString(sPEDC);
			return null;
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

		internal static void Expire(this PwGroup pg)
		{
			PwObjectList<PwEntry> entries = pg.GetEntries(false);
			foreach (PwEntry pe in entries)
				pe.Expire();

			PwObjectList<PwGroup> groups = pg.GetGroups(false);
			foreach (PwGroup g in groups)
				g.Expire();
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
			if (pg.CustomData.Exists(Configuration.Interval))
				return pg.CustomData.Get(Configuration.Interval);
			return pg.CustomData.Get(Configuration.DaysField);
		}

		internal static void SavePEDCString(this PwGroup pg, PEDCalcValue days)
		{
			pg.CustomData.Remove(Configuration.DaysField);
			if (days.Inherit)
				pg.CustomData.Remove(Configuration.Interval);
			else
				pg.CustomData.Set(Configuration.Interval, days.ToString());
			PEDCValueDAO.Invalidate(pg);
		}
	}
}
