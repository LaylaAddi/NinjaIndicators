#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	public class FVGType
	{
		public int StartBar;
		public double Top;
		public double Bottom;
		public bool Bullish;
		public bool Mitigated;
		public string TagTop;
		public string TagBottom;
		public string TagFill;
	}

	public class OrderBlockType
	{
		public int BarIndex;
		public double Top;
		public double Bottom;
		public bool Bullish;
		public bool Mitigated;
		public string Tag;
	}

	public class SRLevelType
	{
		public int BarIndex;
		public double Price;
		public bool IsResistance;
		public int TouchCount;
		public bool Broken;
		public string Tag;
	}

	/// <summary>
	/// Comprehensive Smart Money Concepts indicator: Fair Value Gaps (FVG),
	/// Order Blocks (OB) and Support/Resistance (S/R) zones. Works on any
	/// timeframe/instrument because all detection is based on relative bar
	/// structure (swing pivots, 3-bar gap geometry) rather than fixed price
	/// or tick values.
	/// </summary>
	public class SmartMoneyConcepts : Indicator
	{
		private List<FVGType> bullishFVGs;
		private List<FVGType> bearishFVGs;
		private List<OrderBlockType> bullishOBs;
		private List<OrderBlockType> bearishOBs;
		private List<SRLevelType> srLevels;

		private Series<double> swingHigh;
		private Series<double> swingLow;

		#region Properties

		[NinjaScriptProperty]
		[Display(Name = "Show Fair Value Gaps", Order = 1, GroupName = "Fair Value Gaps")]
		public bool ShowFVG { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name = "Max FVGs To Track", Order = 2, GroupName = "Fair Value Gaps")]
		public int MaxFVGCount { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Remove Mitigated FVGs", Order = 3, GroupName = "Fair Value Gaps")]
		public bool RemoveMitigatedFVG { get; set; }

		[XmlIgnore]
		[Display(Name = "Bullish FVG Color", Order = 4, GroupName = "Fair Value Gaps")]
		public Brush BullishFVGBrush { get; set; }
		[Browsable(false)]
		public string BullishFVGBrushSerialize
		{
			get { return BrushToString(BullishFVGBrush); }
			set { BullishFVGBrush = StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Bearish FVG Color", Order = 5, GroupName = "Fair Value Gaps")]
		public Brush BearishFVGBrush { get; set; }
		[Browsable(false)]
		public string BearishFVGBrushSerialize
		{
			get { return BrushToString(BearishFVGBrush); }
			set { BearishFVGBrush = StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Display(Name = "Show Order Blocks", Order = 10, GroupName = "Order Blocks")]
		public bool ShowOrderBlocks { get; set; }

		[NinjaScriptProperty]
		[Range(2, 50)]
		[Display(Name = "Swing Strength (bars each side)", Order = 11, GroupName = "Order Blocks")]
		public int SwingStrength { get; set; }

		[NinjaScriptProperty]
		[Range(1, 200)]
		[Display(Name = "Max Order Blocks To Track", Order = 12, GroupName = "Order Blocks")]
		public int MaxOBCount { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Remove Mitigated Order Blocks", Order = 13, GroupName = "Order Blocks")]
		public bool RemoveMitigatedOB { get; set; }

		[XmlIgnore]
		[Display(Name = "Bullish OB Color", Order = 14, GroupName = "Order Blocks")]
		public Brush BullishOBBrush { get; set; }
		[Browsable(false)]
		public string BullishOBBrushSerialize
		{
			get { return BrushToString(BullishOBBrush); }
			set { BullishOBBrush = StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Bearish OB Color", Order = 15, GroupName = "Order Blocks")]
		public Brush BearishOBBrush { get; set; }
		[Browsable(false)]
		public string BearishOBBrushSerialize
		{
			get { return BrushToString(BearishOBBrush); }
			set { BearishOBBrush = StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Display(Name = "Show Support/Resistance", Order = 20, GroupName = "Support Resistance")]
		public bool ShowSR { get; set; }

		[NinjaScriptProperty]
		[Range(2, 50)]
		[Display(Name = "Pivot Strength (bars each side)", Order = 21, GroupName = "Support Resistance")]
		public int PivotStrength { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Max S/R Levels To Track", Order = 22, GroupName = "Support Resistance")]
		public int MaxSRCount { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 10.0)]
		[Display(Name = "Level Merge Tolerance (ATR mult)", Order = 23, GroupName = "Support Resistance")]
		public double SRMergeTolerance { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Remove Broken S/R Levels", Order = 24, GroupName = "Support Resistance")]
		public bool RemoveBrokenSR { get; set; }

		[XmlIgnore]
		[Display(Name = "Resistance Color", Order = 25, GroupName = "Support Resistance")]
		public Brush ResistanceBrush { get; set; }
		[Browsable(false)]
		public string ResistanceBrushSerialize
		{
			get { return BrushToString(ResistanceBrush); }
			set { ResistanceBrush = StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Support Color", Order = 26, GroupName = "Support Resistance")]
		public Brush SupportBrush { get; set; }
		[Browsable(false)]
		public string SupportBrushSerialize
		{
			get { return BrushToString(SupportBrush); }
			set { SupportBrush = StringToBrush(value); }
		}

		#endregion

		private static string BrushToString(Brush brush)
		{
			return brush == null ? string.Empty : brush.ToString();
		}

		private static Brush StringToBrush(string value)
		{
			if (string.IsNullOrEmpty(value))
				return null;
			Brush brush = (Brush)new BrushConverter().ConvertFromString(value);
			brush.Freeze();
			return brush;
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "Plots Fair Value Gaps, Order Blocks and Support/Resistance levels. Works on any timeframe.";
				Name = "SmartMoneyConcepts";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				DisplayInDataBox = false;
				DrawOnPricePanel = true;
				IsSuspendedWhileInactive = true;

				ShowFVG = true;
				MaxFVGCount = 50;
				RemoveMitigatedFVG = false;
				BullishFVGBrush = Brushes.LimeGreen;
				BearishFVGBrush = Brushes.OrangeRed;

				ShowOrderBlocks = true;
				SwingStrength = 5;
				MaxOBCount = 30;
				RemoveMitigatedOB = false;
				BullishOBBrush = Brushes.DodgerBlue;
				BearishOBBrush = Brushes.MediumVioletRed;

				ShowSR = true;
				PivotStrength = 10;
				MaxSRCount = 20;
				SRMergeTolerance = 0.5;
				RemoveBrokenSR = false;
				ResistanceBrush = Brushes.Red;
				SupportBrush = Brushes.DeepSkyBlue;
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded)
			{
				bullishFVGs = new List<FVGType>();
				bearishFVGs = new List<FVGType>();
				bullishOBs = new List<OrderBlockType>();
				bearishOBs = new List<OrderBlockType>();
				srLevels = new List<SRLevelType>();

				swingHigh = new Series<double>(this);
				swingLow = new Series<double>(this);
			}
		}

		protected override void OnBarUpdate()
		{
			int lookback = Math.Max(SwingStrength, PivotStrength) + 2;
			if (CurrentBar < lookback)
				return;

			if (ShowFVG)
				ProcessFairValueGaps();

			if (ShowOrderBlocks)
				ProcessOrderBlocks();

			if (ShowSR)
				ProcessSupportResistance();
		}

		#region Fair Value Gaps

		private void ProcessFairValueGaps()
		{
			// Bullish FVG: gap between High[2] and Low[0] (3-candle pattern), Low[0] > High[2]
			if (Low[0] > High[2])
			{
				FVGType fvg = new FVGType
				{
					StartBar = CurrentBar - 2,
					Top = Low[0],
					Bottom = High[2],
					Bullish = true,
					Mitigated = false,
					TagTop = "FVGBullTop" + CurrentBar,
					TagFill = "FVGBullFill" + CurrentBar
				};
				bullishFVGs.Add(fvg);
			}

			// Bearish FVG: gap between Low[2] and High[0], High[0] < Low[2]
			if (High[0] < Low[2])
			{
				FVGType fvg = new FVGType
				{
					StartBar = CurrentBar - 2,
					Top = Low[2],
					Bottom = High[0],
					Bullish = false,
					Mitigated = false,
					TagTop = "FVGBearTop" + CurrentBar,
					TagFill = "FVGBearFill" + CurrentBar
				};
				bearishFVGs.Add(fvg);
			}

			UpdateFVGList(bullishFVGs);
			UpdateFVGList(bearishFVGs);

			if (bullishFVGs.Count > MaxFVGCount)
				bullishFVGs.RemoveRange(0, bullishFVGs.Count - MaxFVGCount);
			if (bearishFVGs.Count > MaxFVGCount)
				bearishFVGs.RemoveRange(0, bearishFVGs.Count - MaxFVGCount);

			DrawFVGs(bullishFVGs, BullishFVGBrush);
			DrawFVGs(bearishFVGs, BearishFVGBrush);
		}

		private void UpdateFVGList(List<FVGType> list)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				FVGType fvg = list[i];
				if (fvg.Mitigated)
					continue;

				bool filled = fvg.Bullish ? (Low[0] <= fvg.Bottom) : (High[0] >= fvg.Top);
				if (filled)
				{
					fvg.Mitigated = true;
					if (RemoveMitigatedFVG)
					{
						RemoveDrawObject(fvg.TagFill);
						list.RemoveAt(i);
					}
				}
			}
		}

		private void DrawFVGs(List<FVGType> list, Brush brush)
		{
			foreach (FVGType fvg in list)
			{
				if (fvg.Mitigated && RemoveMitigatedFVG)
					continue;

				Brush fillBrush = brush.Clone();
				fillBrush.Opacity = fvg.Mitigated ? 0.08 : 0.25;

				int startBarsAgo = CurrentBar - fvg.StartBar;
				Draw.Rectangle(this, fvg.TagFill, false, startBarsAgo, fvg.Top,
					0, fvg.Bottom, brush, fillBrush, 70);
			}
		}

		#endregion

		#region Order Blocks

		private void ProcessOrderBlocks()
		{
			int s = SwingStrength;
			int pivotBar = CurrentBar - s;
			if (pivotBar - s < 0)
				return;

			bool isSwingHigh = true;
			bool isSwingLow = true;
			double pivotHigh = High[CurrentBar - pivotBar];
			double pivotLow = Low[CurrentBar - pivotBar];

			for (int i = 1; i <= s; i++)
			{
				int leftOffset = CurrentBar - (pivotBar - i);
				int rightOffset = CurrentBar - (pivotBar + i);
				if (High[leftOffset] > pivotHigh || High[rightOffset] > pivotHigh)
					isSwingHigh = false;
				if (Low[leftOffset] < pivotLow || Low[rightOffset] < pivotLow)
					isSwingLow = false;
			}

			int pivotOffset = CurrentBar - pivotBar;

			// Bearish order block: swing high followed by strong move down -> the last up-close candle before the drop
			if (isSwingHigh)
			{
				int obOffset = FindLastOppositeCandle(pivotOffset, false);
				if (obOffset >= 0)
				{
					OrderBlockType ob = new OrderBlockType
					{
						BarIndex = CurrentBar - obOffset,
						Top = High[obOffset],
						Bottom = Low[obOffset],
						Bullish = false,
						Mitigated = false,
						Tag = "OBBear" + (CurrentBar - obOffset)
					};
					if (!OBExists(bearishOBs, ob.BarIndex))
						bearishOBs.Add(ob);
				}
			}

			// Bullish order block: swing low followed by strong move up -> last down-close candle before the rally
			if (isSwingLow)
			{
				int obOffset = FindLastOppositeCandle(pivotOffset, true);
				if (obOffset >= 0)
				{
					OrderBlockType ob = new OrderBlockType
					{
						BarIndex = CurrentBar - obOffset,
						Top = High[obOffset],
						Bottom = Low[obOffset],
						Bullish = true,
						Mitigated = false,
						Tag = "OBBull" + (CurrentBar - obOffset)
					};
					if (!OBExists(bullishOBs, ob.BarIndex))
						bullishOBs.Add(ob);
				}
			}

			UpdateOBList(bullishOBs);
			UpdateOBList(bearishOBs);

			if (bullishOBs.Count > MaxOBCount)
				bullishOBs.RemoveRange(0, bullishOBs.Count - MaxOBCount);
			if (bearishOBs.Count > MaxOBCount)
				bearishOBs.RemoveRange(0, bearishOBs.Count - MaxOBCount);

			DrawOBs(bullishOBs, BullishOBBrush);
			DrawOBs(bearishOBs, BearishOBBrush);
		}

		private bool OBExists(List<OrderBlockType> list, int barIndex)
		{
			foreach (OrderBlockType ob in list)
				if (ob.BarIndex == barIndex)
					return true;
			return false;
		}

		// Walk forward (toward bar 0) from the pivot looking for the last candle
		// whose close direction is opposite to the impulse direction we expect.
		// wantBullishOB = true => looking for a down-close candle just before an up move (bullish OB)
		private int FindLastOppositeCandle(int pivotOffset, bool wantBullishOB)
		{
			int searchRange = Math.Min(pivotOffset, 10);
			for (int offset = pivotOffset; offset > pivotOffset - searchRange && offset >= 0; offset--)
			{
				bool downClose = Close[offset] < Open[offset];
				bool upClose = Close[offset] > Open[offset];

				if (wantBullishOB && downClose)
					return offset;
				if (!wantBullishOB && upClose)
					return offset;
			}
			return -1;
		}

		private void UpdateOBList(List<OrderBlockType> list)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				OrderBlockType ob = list[i];
				if (ob.Mitigated)
					continue;

				bool mitigated = ob.Bullish ? (Close[0] < ob.Bottom) : (Close[0] > ob.Top);
				if (mitigated)
				{
					ob.Mitigated = true;
					if (RemoveMitigatedOB)
					{
						RemoveDrawObject(ob.Tag);
						list.RemoveAt(i);
					}
				}
			}
		}

		private void DrawOBs(List<OrderBlockType> list, Brush brush)
		{
			foreach (OrderBlockType ob in list)
			{
				if (ob.Mitigated && RemoveMitigatedOB)
					continue;

				int startOffset = CurrentBar - ob.BarIndex;
				Brush fillBrush = brush.Clone();
				fillBrush.Opacity = ob.Mitigated ? 0.08 : 0.20;

				Draw.Rectangle(this, ob.Tag, false, startOffset, ob.Top, 0, ob.Bottom, brush, fillBrush, 70);
			}
		}

		#endregion

		#region Support / Resistance

		private void ProcessSupportResistance()
		{
			int p = PivotStrength;
			int pivotBar = CurrentBar - p;
			if (pivotBar - p < 0)
				return;

			int pivotOffset = CurrentBar - pivotBar;
			double pivotHigh = High[pivotOffset];
			double pivotLow = Low[pivotOffset];

			bool isSwingHigh = true;
			bool isSwingLow = true;
			for (int i = 1; i <= p; i++)
			{
				int leftOffset = pivotOffset - i;
				int rightOffset = pivotOffset + i;
				if (High[leftOffset] > pivotHigh || High[rightOffset] > pivotHigh)
					isSwingHigh = false;
				if (Low[leftOffset] < pivotLow || Low[rightOffset] < pivotLow)
					isSwingLow = false;
			}

			double atrVal = ATR(14)[0];
			double tolerance = atrVal * SRMergeTolerance;

			if (isSwingHigh)
				AddOrMergeLevel(pivotHigh, pivotBar, true, tolerance);

			if (isSwingLow)
				AddOrMergeLevel(pivotLow, pivotBar, false, tolerance);

			UpdateSRLevels();

			if (srLevels.Count > MaxSRCount)
			{
				srLevels.Sort((a, b) => b.TouchCount.CompareTo(a.TouchCount));
				for (int i = MaxSRCount; i < srLevels.Count; i++)
					RemoveDrawObject(srLevels[i].Tag);
				srLevels.RemoveRange(MaxSRCount, srLevels.Count - MaxSRCount);
			}

			DrawSRLevels();
		}

		private void AddOrMergeLevel(double price, int barIndex, bool isResistance, double tolerance)
		{
			foreach (SRLevelType lvl in srLevels)
			{
				if (lvl.IsResistance == isResistance && Math.Abs(lvl.Price - price) <= tolerance && !lvl.Broken)
				{
					lvl.TouchCount++;
					lvl.Price = (lvl.Price + price) / 2.0;
					return;
				}
			}

			SRLevelType newLevel = new SRLevelType
			{
				BarIndex = barIndex,
				Price = price,
				IsResistance = isResistance,
				TouchCount = 1,
				Broken = false,
				Tag = (isResistance ? "SRRes" : "SRSup") + barIndex
			};
			srLevels.Add(newLevel);
		}

		private void UpdateSRLevels()
		{
			for (int i = srLevels.Count - 1; i >= 0; i--)
			{
				SRLevelType lvl = srLevels[i];
				if (lvl.Broken)
					continue;

				bool broken = lvl.IsResistance ? (Close[0] > lvl.Price) : (Close[0] < lvl.Price);
				if (broken)
				{
					lvl.Broken = true;
					if (RemoveBrokenSR)
					{
						RemoveDrawObject(lvl.Tag);
						srLevels.RemoveAt(i);
					}
				}
			}
		}

		private void DrawSRLevels()
		{
			foreach (SRLevelType lvl in srLevels)
			{
				if (lvl.Broken && RemoveBrokenSR)
					continue;

				Brush brush = lvl.IsResistance ? ResistanceBrush : SupportBrush;
				int startOffset = CurrentBar - lvl.BarIndex;
				DashStyleHelper dash = lvl.Broken ? DashStyleHelper.Dot : DashStyleHelper.Solid;
				int width = Math.Min(1 + lvl.TouchCount, 4);

				Draw.Line(this, lvl.Tag, false, startOffset, lvl.Price, 0, lvl.Price, brush, dash, width);
			}
		}

		#endregion
	}
}
