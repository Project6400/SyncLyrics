using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Amaoto;
using Koioto.Support;
using Koioto.Support.FileReader;
using Koioto.Support.Log;
using Space.AioiLight.LRCDotNet;

namespace SyncLyrics
{
    public class SyncLyrics : Koioto.Plugin.Overlay
    {
        public override string Name => "SyncLyrics";
        public override string[] Creator => new string[] { "AioiLight" };
        public override string Version => "1.0";
        public override string Description => "Show sync lyrics (*.lrc) at playing screen.";

        public override void OnEnable()
        {
            LyricFont = new FontRender(new FontFamily(Bridge.Settings.Font), 36, 8);
            base.OnEnable();
        }

        public override void OnDisable()
        {
            Lyric = null;
            LyricFont = null;
            LyricAndTimings = null;
            base.OnDisable();
        }
        public override void OnSelectedSong(Playable[] playable, ChartInfo chartInfo, PlayLog[] playLogs)
        {
            // Use player1's Playable
            var p = playable[0];

            Lyric = null;
            Lyric = new LyRiCs[p.Sections.Length];

            // *.lrc parse phase
            for (int section = 0; section < Lyric.Length; section++)
            {
                // get path for file
                var audioPath = chartInfo.Audio[section];

                if (audioPath == null)
                {
                    continue;
                }

                var folder = Path.GetDirectoryName(audioPath);
                var lrcFile = $"{Path.GetFileNameWithoutExtension(audioPath)}.lrc";

                var lrcPath = Path.Combine(folder, lrcFile);

                // read and parse
                if (!File.Exists(lrcPath))
                {
                    continue;
                }

                var file = File.ReadAllText(lrcPath);

                var result = LRCDotNet.Parse(file);

                Lyric[section] = result;
            }
            // phase end

            // Generation texture phase
            LyricAndTimings = new LyricAndTiming[Lyric.Length][];
            for (int section = 0; section < Lyric.Length; section++)
            {
                if (Lyric[section] == null)
                {
                    continue;
                }

                var lyric = Lyric[section].Lyrics;
                LyricAndTimings[section] = new LyricAndTiming[lyric.Count()];

                for (int l = 0; l < lyric.Count(); l++)
                {
                    // convert ms to ns
                    var timing = (long)(lyric[l].Time.TotalMilliseconds * 1000.0);
                    var tex = LyricFont.GetTexture(lyric[l].Text);
                    LyricAndTimings[section][l] = new LyricAndTiming(tex, timing);
                }
            }
            // phase end

            Showing = null;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        public override void OnDraw()
        {
            if (Showing != null)
            {
                Showing.ReferencePoint = ReferencePoint.BottomCenter;
                Showing.Draw(ScreenSize.Width / 2, ScreenSize.Height);
            }
        }

        public override void OnPlayer(long sectionTickValue)
        {
            if (LyricAndTimings[SectionIndex] == null)
            {
                return;
            }

            // calc accurate time from lag
            var accuTime = sectionTickValue - Lag;

            if (LyricAndTimings[SectionIndex].Length <= LyricIndex + 1)
            {
                return;
            }

            if (!ShowedFirstLyric)
            {
                if (accuTime >= LyricAndTimings[SectionIndex][0].Timing)
                {
                    Showing = LyricAndTimings[SectionIndex][0].Tex;
                    ShowedFirstLyric = true;
                    return;
                }
            }

            var nextLyric = LyricAndTimings[SectionIndex][LyricIndex + 1];
            
            if (accuTime >= nextLyric.Timing)
            {
                // set texture
                LyricIndex++;
                Showing = nextLyric.Tex;
            }
        }

        public override void OnChangedSection(int sectionIndex, int player, List<Chip> section)
        {
            // reset some vars
            if (player == 0)
            {
                SectionIndex = sectionIndex;
                LyricIndex = 0;
                Showing = null;
                ShowedFirstLyric = false;

                // get time until starts bgm
                Lag = section.First(c => c.ChipType == Chips.BGMStart).Time;
            }
        }

        public override void OnChangedResolution(Size size)
        {
            ScreenSize = size;
        }

        private LyRiCs[] Lyric;
        private FontRender LyricFont;
        private int SectionIndex;
        private int LyricIndex;
        private Texture Showing;
        private LyricAndTiming[][] LyricAndTimings;
        private bool ShowedFirstLyric;
        private Size ScreenSize;

        private long Lag;
    }

    internal class LyricAndTiming
    {
        internal LyricAndTiming(Texture tex, long timing)
        {
            Tex = tex;
            Timing = timing;
        }

        internal Texture Tex { get; private set; }
        internal long Timing { get; private set; }
    }
}
