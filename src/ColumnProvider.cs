using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KeePassLib;
using PluginTools;
using PluginTranslation;

namespace PEDCalc
{
	class PEDCalcColumnProvider: KeePass.UI.ColumnProvider
	{
		private static string[] m_ColNames = new string[] { "PEDCalc" };
		public override string[] ColumnNames { get { return m_ColNames; } }

		public override string GetCellData(string strColumnName, PwEntry pe)
		{
			return PEDCValueDAO.GetPEDCValueString(pe);
		}

		public override bool SupportsCellAction(string strColumnName)
		{
			return true;
		}

		public override void PerformCellAction(string strColumnName, PwEntry pe)
		{
			pe.RecalcExpiry(true);
		}
	}
}
