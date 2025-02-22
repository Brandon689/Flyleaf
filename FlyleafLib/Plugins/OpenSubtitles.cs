﻿using System;
using System.IO;

using FlyleafLib.MediaFramework.MediaStream;
using FlyleafLib.MediaFramework.MediaPlaylist;

namespace FlyleafLib.Plugins;

public class OpenSubtitles : PluginBase, IOpenSubtitles, ISearchLocalSubtitles
{
    public new int Priority { get; set; } = 3000;

    public OpenSubtitlesResults Open(string url)
    {
        /* TODO
         * 1) Identify language
         */

        foreach(var extStream in Selected.ExternalSubtitlesStreams)
            if (extStream.Url == url)
                return new OpenSubtitlesResults(extStream);

        string title;

        try
        {
            FileInfo fi = new(url);
            title = fi.Extension == null ? fi.Name : fi.Name[..^fi.Extension.Length];
        }
        catch { title = url; }
                
        ExternalSubtitlesStream newExtStream = new()
        {
            Url         = url,
            Title       = title,
            Downloaded  = true
        };

        AddExternalStream(newExtStream);

        return new OpenSubtitlesResults(newExtStream);
    }

    public OpenSubtitlesResults Open(Stream iostream) => null;

    public void SearchLocalSubtitles()
    {
        /* TODO
         * 1) Subs folder could exist under Season X (it will suggest another season's subtitle)
         * 2) Identify language
         * 3) Confidence
         */

        try
        {
            string folder = Path.Combine(Playlist.FolderBase, Selected.Folder, "Subs");
            if (!Directory.Exists(folder))
                return;

            string[] filesCur = Directory.GetFiles(folder, $"*.srt"); // We consider Subs/ folder has only subs for this movie/series

            foreach(string file in filesCur)
            {
                bool exists = false;
                foreach(var extStream in Selected.ExternalSubtitlesStreams)
                    if (extStream.Url == file)
                        { exists = true; break; }
                if (exists) continue;

                FileInfo fi = new(file);

                // We might have same Subs/ folder for more than one episode/season then filename requires to have season/episode
                var mp = Utils.GetMediaParts(fi.Name);
                if (mp.Episode != Selected.Episode || (mp.Season != Selected.Season && Selected.Season > 0 && mp.Season > 0))
                    continue;

                string title = fi.Extension == null ? fi.Name : fi.Name[..^fi.Extension.Length];

                // Until we analyze the actual text to identify the language we just use the filename
                bool converted = false;
                var lang = Language.Unknown;

                if (fi.Name.Contains("utf8"))
                {
                    int pos = -1;
                    foreach (var lang2 in Config.Subtitles.Languages)
                        if ((pos = fi.Name.IndexOf($"{lang2.IdSubLanguage}.utf8") - 1) > 0)
                            { lang = lang2; converted = true; title = fi.Name[..(pos - 1)]; break; }
                }
                Log.Debug($"Adding [{lang}] {file}");

                AddExternalStream(new ExternalSubtitlesStream()
                {
                    Url         = file,
                    Title       = title,
                    Converted   = converted,
                    Downloaded  = true,
                    Language    = lang
                });
            }
        } catch (Exception e) { Log.Error($"SearchLocalSubtitles failed ({e.Message})"); }
    }
}
