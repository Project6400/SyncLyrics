using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Amaoto;
using Koioto.Plugin;
using Koioto.Support;
using Koioto.Support.FileReader;
using Koioto.Support.Log;
using Space.AioiLight.LRCDotNet;

namespace Koioto.SamplePlugin.SyncLyrics
{
	// Token: 0x02000002 RID: 2
	public class SyncLyrics : Overlay
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000001 RID: 1
		public override string Name
		{
			get
			{
				return "SyncLyrics";
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000002 RID: 2
		public override string[] Creator
		{
			get
			{
				return new string[]
				{
					"AioiLight"
				};
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000003 RID: 3
		public override string Version
		{
			get
			{
				return "1.1";
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000004 RID: 4
		public override string Description
		{
			get
			{
				return "Show sync lyrics (*.lrc) at playing screen.";
			}
		}
		// Token: 0x06000005 RID: 5
		public override void OnEnable()
		{
			StreamReader streamReader = new StreamReader(Bridge.PluginDir + "\\LyricsFont.txt", Encoding.GetEncoding("UTF-8"));
			string LyricsFont = streamReader.ReadLine();
			string LyricsFontSize = streamReader.ReadLine();
			string LyricsFontEdge = streamReader.ReadLine();
			int s = Convert.ToInt32(LyricsFontSize);
			int e = Convert.ToInt32(LyricsFontEdge);
			streamReader.Close();
			LyricFont = new FontRender(new FontFamily(LyricsFont), s, e, FontStyle.Regular) ;
			base.OnEnable();
		}
		// Token: 0x06000006 RID: 6
		public override void OnDisable()
		{
			this.Lyric = null;
			this.LyricFont = null;
			this.LyricAndTimings = null;
			base.OnDisable();
		}

		// Token: 0x06000007 RID: 7
		public override void OnSelectedSong(Playable[] playable, ChartInfo chartInfo, PlayLog[] playLogs)
		{
			Playable p = playable[0];
			this.Lyric = null;
			this.Lyric = new LyRiCs[p.Sections.Length];
			for (int section = 0; section < this.Lyric.Length; section++)
			{
				string audioPath = chartInfo.Audio[section];
				if (audioPath != null)
				{
					string directoryName = Path.GetDirectoryName(audioPath);
					string lrcFile = Path.GetFileNameWithoutExtension(audioPath) + ".lrc";
					string lrcPath = Path.Combine(directoryName, lrcFile);
					if (File.Exists(lrcPath))
					{
						LyRiCs result = LRCDotNet.Parse(File.ReadAllText(lrcPath));
						this.Lyric[section] = result;
					}
				}
			}
			this.LyricAndTimings = new SyncLyrics.LyricAndTiming[this.Lyric.Length][];
			for (int section2 = 0; section2 < this.Lyric.Length; section2++)
			{
				if (this.Lyric[section2] != null)
				{
					List<Lyric> lyric = this.Lyric[section2].Lyrics;
					this.LyricAndTimings[section2] = new SyncLyrics.LyricAndTiming[lyric.Count<Lyric>()];
					for (int i = 0; i < lyric.Count<Lyric>(); i++)
					{
						long timing = (long)(lyric[i].Time.TotalMilliseconds * 1000.0);
						Texture tex = this.LyricFont.GetTexture(lyric[i].Text, null);
						this.LyricAndTimings[section2][i] = new SyncLyrics.LyricAndTiming(tex, timing);
					}
				}
			}
			this.Showing = null;
		}

		// Token: 0x06000008 RID: 8
		public override void OnUpdate()
		{
			base.OnUpdate();
		}

		// Token: 0x06000009 RID: 9
		public override void OnDraw()
		{
			if (this.Showing != null)
			{
				this.Showing.ReferencePoint = ReferencePoint.BottomCenter;
				this.Showing.Draw((float)(this.ScreenSize.Width / 2), (float)this.ScreenSize.Height, null);
			}
		}

		// Token: 0x0600000A RID: 10
		public override void OnPlayer(long sectionTickValue)
		{
			if (this.LyricAndTimings[this.SectionIndex] == null)
			{
				return;
			}
			long accuTime = sectionTickValue - this.Lag;
			if (sectionTickValue < this.Counter)
			{
				SyncLyrics.LyricAndTiming last = (from t in this.LyricAndTimings[this.SectionIndex]
												  where t.Timing <= accuTime
												  select t).LastOrDefault<SyncLyrics.LyricAndTiming>();
				this.Showing = ((last != null) ? last.Tex : null);
				this.LyricIndex = this.LyricAndTimings[this.SectionIndex].ToList<SyncLyrics.LyricAndTiming>().IndexOf(last);
				this.Counter = sectionTickValue;
				return;
			}
			if (this.LyricAndTimings[this.SectionIndex].Length <= this.LyricIndex + 1)
			{
				return;
			}
			if (!this.ShowedFirstLyric && accuTime >= this.LyricAndTimings[this.SectionIndex][0].Timing)
			{
				this.Showing = this.LyricAndTimings[this.SectionIndex][0].Tex;
				this.ShowedFirstLyric = true;
				return;
			}
			SyncLyrics.LyricAndTiming nextLyric = this.LyricAndTimings[this.SectionIndex][this.LyricIndex + 1];
			if (accuTime >= nextLyric.Timing)
			{
				this.LyricIndex++;
				this.Showing = nextLyric.Tex;
			}
			this.Counter = sectionTickValue;
		}

		// Token: 0x0600000B RID: 11
		public override void OnChangedSection(int sectionIndex, int player, List<Chip> section)
		{
			if (player == 0)
			{
				this.SectionIndex = sectionIndex;
				this.LyricIndex = 0;
				this.Showing = null;
				this.ShowedFirstLyric = false;
				this.Lag = section.First((Chip c) => c.ChipType == Chips.BGMStart).Time;
			}
		}

		// Token: 0x0600000C RID: 12
		public override void OnChangedResolution(Size size)
		{
			this.ScreenSize = size;
		}

		// Token: 0x04000001 RID: 1
		private LyRiCs[] Lyric;

		// Token: 0x04000002 RID: 2
		private FontRender LyricFont;

		// Token: 0x04000003 RID: 3
		private int SectionIndex;

		// Token: 0x04000004 RID: 4
		private int LyricIndex;

		// Token: 0x04000005 RID: 5
		private Texture Showing;

		// Token: 0x04000007 RID: 7
		private bool ShowedFirstLyric;

		// Token: 0x04000008 RID: 8
		private Size ScreenSize;

		// Token: 0x04000009 RID: 9
		private long Lag;

		// Token: 0x0400000A RID: 10
		private long Counter;

		// Token: 0x04000077 RID: 119
		private SyncLyrics.LyricAndTiming[][] LyricAndTimings;
		// Token: 0x02000006 RID: 6
		internal class LyricAndTiming
		{
			// Token: 0x0600008F RID: 143
			internal LyricAndTiming(Texture tex, long timing)
			{
				this.Tex = tex;
				this.Timing = timing;
			}

			// Token: 0x17000023 RID: 35
			// (get) Token: 0x06000090 RID: 144
			// (set) Token: 0x06000091 RID: 145
			internal Texture Tex { get; private set; }

			// Token: 0x17000024 RID: 36
			// (get) Token: 0x06000092 RID: 146
			// (set) Token: 0x06000093 RID: 147
			internal long Timing { get; private set; }
		}
	}
}
