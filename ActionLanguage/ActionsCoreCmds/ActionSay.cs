﻿/*
 * Copyright © 2017 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using System;
using System.Collections.Generic;
using BaseUtils;
using AudioExtensions;

namespace ActionLanguage
{
    public class ActionSay : ActionBase
    {
        public override bool AllowDirectEditingOfUserData { get { return true; } }    // and allow editing?

        public static string globalvarspeechvolume = "SpeechVolume";
        public static string globalvarspeechrate = "SpeechRate";
        public static string globalvarspeechvoice = "SpeechVoice";
        public static string globalvarspeecheffects = "SpeechEffects";
        public static string globalvarspeechculture = "SpeechCulture";
        public static string globalvarspeechpriority = "SpeechPriority";
        public static string globalvarspeechdisable = "SpeechDisable";

        static string volumename = "Volume";
        static string voicename = "Voice";
        static string ratename = "Rate";
        static string waitname = "Wait";
        static string priorityname = "Priority";
        static string startname = "StartEvent";
        static string finishname = "FinishEvent";
        static string culturename = "Culture";
        static string literalname = "Literal";
        static string dontspeakname = "DontSpeak";
        static string prefixsound = "PrefixSound";
        static string postfixsound = "PostfixSound";
        static string mixsound = "MixSound";
        static string queuelimit = "QueueLimit";

        static public bool FromString(string s, out string saying, out Variables vars)
        {
            vars = new Variables();

            StringParser p = new StringParser(s);
            saying = p.NextQuotedWord(", ");        // stop at space or comma..

            if (saying != null && (p.IsEOL || (p.IsCharMoveOn(',') && vars.FromString(p, Variables.FromMode.MultiEntryComma))))   // normalise variable names (true)
                return true;

            saying = "";
            return false;
        }

        static public string ToString(string saying, Variables cond)
        {
            if (cond.Count > 0)
                return saying.QuoteString(comma: true) + ", " + cond.ToString();
            else
                return saying.QuoteString(comma: true);
        }

        public override string VerifyActionCorrect()
        {
            string saying;
            Variables vars;
            return FromString(userdata, out saying, out vars) ? null : "Say command line not in correct format";
        }

        static public string Configure(string userdata, ActionCoreController cp, ActionConfigFuncs configFuncs)
        {
            string saying;
            Variables vars;
            FromString(userdata, out saying, out vars);

            bool success = configFuncs.ConfigureSpeech(
                        cp.AudioQueueSpeech,
                        cp.SpeechSynthesizer,
                        "Set Text to say (use ; to separate randomly selectable phrases and {} to group)", "Configure Say Command", cp.Icon,
                        saying,
                        vars.Exists(waitname), vars.Exists(literalname),
                        AudioQueue.GetPriority(vars.GetString(priorityname, "Normal")),
                        vars.GetString(startname, ""),
                        vars.GetString(finishname, ""),
                        vars.GetString(voicename, "Default"),
                        vars.GetString(volumename, "Default"),
                        vars.GetString(ratename, "Default"),
                        vars,
                        cfg =>
                        {
                            Variables cond = new Variables(cfg.Effects);// add on any effects variables (and may add in some previous variables, since we did not purge
                            cond.SetOrRemove(cfg.Wait, waitname, "1");
                            cond.SetOrRemove(cfg.Literal, literalname, "1");
                            cond.SetOrRemove(cfg.Priority != AudioQueue.Priority.Normal, priorityname, cfg.Priority.ToString());
                            cond.SetOrRemove(cfg.StartEvent.Length > 0, startname, cfg.StartEvent);
                            cond.SetOrRemove(cfg.StartEvent.Length > 0, finishname, cfg.FinishEvent);
                            cond.SetOrRemove(!cfg.VoiceName.Equals("Default", StringComparison.InvariantCultureIgnoreCase), voicename, cfg.VoiceName);
                            cond.SetOrRemove(!cfg.Volume.Equals("Default", StringComparison.InvariantCultureIgnoreCase), volumename, cfg.Volume);
                            cond.SetOrRemove(!cfg.Rate.Equals("Default", StringComparison.InvariantCultureIgnoreCase), ratename, cfg.Rate);
                            userdata = ToString(cfg.SayText, cond);
                        }
            );

            return success ? userdata : null;
        }


        public override bool Configure(ActionCoreController cp, List<TypeHelpers.PropertyNameInfo> eventvars, ActionConfigFuncs configFuncs)
        {
            var ud = Configure(userdata, cp, configFuncs);
            if (ud != null)
            {
                userdata = ud;
                return true;
            }
            else
            {
                return false;
            }
        }

        class AudioEvent
        {
            public ActionProgramRun apr;
            public bool wait;
            public ActionEvent ev;
            public string eventname;
        }

        public override bool ExecuteAction(ActionProgramRun ap)
        {
            string say;
            Variables statementvars;
            if (FromString(userdata, out say, out statementvars))
            {
                string ctrl = ap.VarExist("SpeechDebug") ? ap["SpeechDebug"] : "";

                string errlist = null;
                Variables vars = ap.functions.ExpandVars(statementvars, out errlist);

                if (ctrl.Contains("SayLine"))
                {
                    ap.actioncontroller.LogLine("Say Command: " + userdata);
                    ap.actioncontroller.LogLine("Say Vars: " + vars.ToString(separ: Environment.NewLine));
                    System.Diagnostics.Debug.WriteLine("Say Vars: " + vars.ToString(separ: Environment.NewLine));
                }

                if (errlist == null)
                {
                    bool wait = vars.GetInt(waitname, 0) != 0;

                    string prior = (vars.Exists(priorityname) && vars[priorityname].Length > 0) ? vars[priorityname] : (ap.VarExist(globalvarspeechpriority) ? ap[globalvarspeechpriority] : "Normal");
                    AudioQueue.Priority priority = AudioQueue.GetPriority(prior);

                    string start = vars.GetString(startname, checklen: true);
                    string finish = vars.GetString(finishname, checklen: true);
                    string voice = (vars.Exists(voicename) && vars[voicename].Length>0)? vars[voicename] : (ap.VarExist(globalvarspeechvoice) ? ap[globalvarspeechvoice] : "Default");

                    int vol = vars.GetInt(volumename, -999);
                    if (vol == -999)
                        vol = ap.variables.GetInt(globalvarspeechvolume, 60);

                    int rate = vars.GetInt(ratename, -999);
                    if (rate == -999)
                        rate = ap.variables.GetInt(globalvarspeechrate, 0);

                    int queuelimitms = vars.GetInt(queuelimit, 0);

                    string culture = (vars.Exists(culturename) && vars[culturename].Length > 0) ? vars[culturename] : (ap.VarExist(globalvarspeechculture) ? ap[globalvarspeechculture] : "Default");

                    bool literal = vars.GetInt(literalname, 0) != 0;
                    bool dontspeak = vars.Exists(dontspeakname) ? (vars.GetInt(dontspeakname, 0) != 0) : (ap.VarExist(globalvarspeechdisable) ? ap[globalvarspeechdisable].InvariantParseInt(0) != 0 : false);

                    string prefixsoundpath = vars.GetString(prefixsound, checklen: true);
                    string postfixsoundpath = vars.GetString(postfixsound, checklen: true);
                    string mixsoundpath = vars.GetString(mixsound, checklen: true);

                    Variables globalsettings = ap.VarExist(globalvarspeecheffects) ? new Variables(ap[globalvarspeecheffects], Variables.FromMode.MultiEntryComma) : null;
                    SoundEffectSettings ses = SoundEffectSettings.Set(globalsettings, vars);        // work out the settings

                    if (queuelimitms > 0)
                    {
                        int queue = ap.actioncontroller.AudioQueueSpeech.InQueuems();

                        if (queue >= queuelimitms)
                        {
                            ap["SaySaid"] = "!LIMIT";
                            System.Diagnostics.Debug.WriteLine("Abort say due to queue being at " + queue);
                            return true;
                        }
                    }

                    string expsay;
                    if (ap.functions.ExpandString(say, out expsay) != Functions.ExpandResult.Failed)
                    {
                        System.Diagnostics.Debug.WriteLine("Say wait {0}, vol {1}, rate {2}, queue {3}, priority {4}, culture {5}, literal {6}, dontspeak {7} , prefix {8}, postfix {9}, mix {10} starte {11}, finishe {12} , voice {13}, text {14}",
                                        wait, vol, rate, queuelimitms, priority, culture, literal, dontspeak, prefixsoundpath, postfixsoundpath, mixsoundpath, start, finish, voice , expsay);

                        Random rnd = FunctionHandlers.GetRandom();

                        if ( !literal )
                        {
                            expsay = expsay.PickOneOfGroups(rnd);       // expand grouping if not literal
                        }

                        ap["SaySaid"] = expsay;

                        if (ctrl.Contains("Global"))
                        {
                            ap.actioncontroller.SetPeristentGlobal("GlobalSaySaid", expsay);
                        }

                        if (ctrl.Contains("Print"))
                        {
                            ap.actioncontroller.LogLine("Say: " + expsay);
                        }

                        if (ctrl.Contains("Mute"))
                        {
                            return true;
                        }

                        if (ctrl.Contains("DontSpeak"))
                        {
                            expsay = "";
                        }

                        if (dontspeak)
                            expsay = "";    

                        System.IO.MemoryStream ms = ap.actioncontroller.SpeechSynthesizer.Speak(expsay, culture, voice, rate);

                        if (ms != null)
                        {
                            AudioQueue.AudioSample audio = ap.actioncontroller.AudioQueueSpeech.Generate(ms, ses, true);

                            if (audio == null)
                            {
                                ap.ReportError("Say could not create audio, check Effects settings");
                                return true;
                            }

                            if (mixsoundpath != null)
                            {
                                AudioQueue.AudioSample mix = ap.actioncontroller.AudioQueueSpeech.Generate(mixsoundpath);

                                if (mix == null)
                                {
                                    ap.ReportError("Say could not create mix audio, check audio file format is supported and effects settings");
                                    return true;
                                }

                                audio = ap.actioncontroller.AudioQueueSpeech.Mix(audio, mix);     // audio in MIX format
                            }

                            if (prefixsoundpath != null)
                            {
                                AudioQueue.AudioSample p = ap.actioncontroller.AudioQueueSpeech.Generate(prefixsoundpath);

                                if ( p == null)
                                {
                                    ap.ReportError("Say could not create prefix audio, check audio file format is supported and effects settings");
                                    return true;
                                }

                                audio = ap.actioncontroller.AudioQueueSpeech.Append(p, audio);        // audio in AUDIO format.
                            }

                            if (postfixsoundpath != null)
                            {
                                AudioQueue.AudioSample p = ap.actioncontroller.AudioQueueSpeech.Generate(postfixsoundpath);

                                if (p == null)
                                {
                                    ap.ReportError("Say could not create postfix audio, check audio file format is supported and effects settings");
                                    return true;
                                }

                                audio = ap.actioncontroller.AudioQueueSpeech.Append(audio,p);         // Audio in P format
                            }

                            if (start != null )
                            {
                                audio.sampleStartTag = new AudioEvent { apr = ap, eventname = start, ev = ActionEvent.onSayStarted };
                                audio.sampleStartEvent += Audio_sampleEvent;
                            }

                            if (wait || finish != null )       // if waiting, or finish call
                            {
                                audio.sampleOverTag = new AudioEvent() { apr = ap, wait = wait, eventname = finish, ev = ActionEvent.onSayFinished };
                                audio.sampleOverEvent += Audio_sampleEvent;
                            }

                            ap.actioncontroller.AudioQueueSpeech.Submit(audio, vol, priority);

                            return !wait;       //False if wait, meaning terminate and wait for it to complete, true otherwise, continue
                        }
                    }
                    else
                        ap.ReportError(expsay);
                }
                else
                    ap.ReportError(errlist);
            }
            else
                ap.ReportError("Say command line not in correct format");


            return true;
        }

        private void Audio_sampleEvent(AudioQueue sender, object tag)
        {
            AudioEvent af = tag as AudioEvent;

            if (af.eventname != null && af.eventname.Length>0)
                af.apr.actioncontroller.ActionRun(af.ev, new Variables("EventName", af.eventname), now: false);    // queue at end an event

            if (af.wait)
                af.apr.ResumeAfterPause();
        }
    }
}
