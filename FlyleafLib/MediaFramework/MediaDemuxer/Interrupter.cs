﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using FFmpeg.AutoGen;

using static FlyleafLib.Logger;

namespace FlyleafLib.MediaFramework.MediaDemuxer;

public unsafe class Interrupter
{
    public int          ForceInterrupt  { get; set; }
    public Requester    Requester       { get; private set; }
    public int          Interrupted     { get; private set; }

    Demuxer demuxer;
    Stopwatch sw = new();
    internal AVIOInterruptCB_callback interruptClbk;

    internal int ShouldInterrupt(void* opaque)
    {
        if (demuxer.Status == Status.Stopping)
        {
            if (CanDebug) demuxer.Log.Debug($"{demuxer.Interrupter.Requester} Interrupt (Stopping) !!!");
            
            return demuxer.Interrupter.Interrupted = 1;
        }

        if (demuxer.Config.AllowTimeouts)
        {
            long curTimeout = 0;
            switch (demuxer.Interrupter.Requester)
            {
                case Requester.Close:
                    curTimeout = demuxer.Config.CloseTimeout;
                    break;

                case Requester.Open:
                    curTimeout = demuxer.Config.OpenTimeout;
                    break;

                case Requester.Read:
                    curTimeout = (demuxer.Duration == 0 || (demuxer.HLSPlaylist != null && demuxer.HLSPlaylist->cur_seq_no > demuxer.HLSPlaylist->last_seq_no - 2)) ? demuxer.Config.ReadLiveTimeout : demuxer.Config.ReadTimeout;
                    break;

                case Requester.Seek:
                    curTimeout = demuxer.Config.SeekTimeout;
                    break;
            }

            if (sw.ElapsedMilliseconds > curTimeout / 10000)
            {
                demuxer.OnTimedOut();

                if (CanWarn) demuxer.Log.Warn($"{demuxer.Interrupter.Requester} Timeout !!!! {sw.ElapsedMilliseconds} ms");

                return demuxer.Interrupter.Interrupted = 1;
            }
        }

        if (demuxer.Interrupter.Requester == Requester.Close) return 0;

        if (demuxer.Interrupter.ForceInterrupt != 0 && demuxer.allowReadInterrupts)
        {
            if (CanTrace) demuxer.Log.Trace($"{demuxer.Interrupter.Requester} Interrupt !!!");
            return demuxer.Interrupter.Interrupted = 1;
        }

        return demuxer.Interrupter.Interrupted = 0;
    }

    public Interrupter(Demuxer demuxer)
    {
        this.demuxer = demuxer;
        interruptClbk = ShouldInterrupt;
    }

    public void Request(Requester requester)
    {
        if (!demuxer.Config.AllowTimeouts) return;

        Requester = requester;
        sw.Restart();
    }
}

public enum Requester
{
    Close,
    Open,
    Read,
    Seek
}
