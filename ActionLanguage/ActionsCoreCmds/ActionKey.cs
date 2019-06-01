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
using System.Collections.Generic;
using BaseUtils;
using AudioExtensions;

namespace ActionLanguage
{
    public class ActionKey: ActionBase
    {
        public static string programDefault = "Program Default";        // meaning use the program default vars for the process

        public static string globalvarProcessID = "KeyProcessTo";       // var global
        protected static string ProcessID = "To";       // command tags

        public static string globalvarDelay = "KeyDelay";
        protected static string DelayID = "Delay";

        public static string globalvarUpDelay = "KeyUpDelay";
        protected static string UpDelayID = "UpDelay";

        public static string globalvarShiftDelay = "KeyShiftDelay";
        protected static string ShiftDelayID = "ShiftDelay";

        public static string globalvarSilentOnErrors = "KeySilentOnError";
        protected static string SilentOnError = "SilentOnError";

        public static string globalvarAnnounciateOnError = "KeyAnnounciateOnError";
        protected static string AnnounciateOnError = "AnnounicateOnError";

        protected const int DefaultDelay = 10;

        static public bool FromString(string s, out string keys, out Variables vars)
        {
            vars = new Variables();

            StringParser p = new StringParser(s);
            keys = p.NextQuotedWord(", ");        // stop at space or comma..

            if (keys != null && (p.IsEOL || (p.IsCharMoveOn(',') && vars.FromString(p, Variables.FromMode.MultiEntryComma))))   // normalise variable names (true)
                return true;

            keys = "";
            return false;
        }

        static public string ToString(string keys, Variables cond )
        {
            if (cond.Count > 0)
                return keys.QuoteString(comma: true) + ", " + cond.ToString();
            else
                return keys.QuoteString(comma: true);
        }

        public override string VerifyActionCorrect()
        {
            string saying;
            Variables vars;
            return FromString(userdata, out saying, out vars) ? null : "Key command line not in correct format";
        }

        public static string Configure(System.Drawing.Icon ic, string userdata, List<string> additionalkeys, IAdditionalKeyParser additionalparser, ActionConfigFuncs configFuncs)
        {
            Variables vars;
            string keys;
            FromString(userdata, out keys, out vars);

            int defdelay = vars.Exists(DelayID) ? vars[DelayID].InvariantParseInt(DefaultDelay) : -1;
            string process = vars.Exists(ProcessID) ? vars[ProcessID] : "";

            string result = null;
            bool success = configFuncs.ConfigureKeys(ic, true, " ", keys, process, defdelay: defdelay, additionalkeys: additionalkeys, parser: additionalparser, resultcb: kf =>
            {
                Variables vlist = new Variables();

                if (kf.DefaultDelay >= 0)                                       // only add these into the command if set to non default
                    vlist[DelayID] = kf.DefaultDelay.ToStringInvariant();
                if (kf.ProcessSelected.Length > 0)
                    vlist[ProcessID] = kf.ProcessSelected;

                result = ToString(kf.KeyList, vlist);
            });

            return success ? result : null;
        }

        public override bool Configure(ActionCoreController cp, List<TypeHelpers.PropertyNameInfo> eventvars, ActionConfigFuncs configFuncs)
        {
            string ud = Configure(cp.Icon, userdata, null, null, configFuncs);      // base has no additional keys/parser
            if (ud != null)
            {
                userdata = ud;
                return true;
            }
            else
                return false;
        }

        public override bool ExecuteAction(ActionProgramRun ap)     // standard action.. at this class level in action language we do not have an additional parser.
        {
            return ExecuteAction(ap, null);
        }

        static List<string> errorsreported = new List<string>();

        public bool ExecuteAction(ActionProgramRun ap, IAdditionalKeyParser akp )      // additional parser
        { 
            string keys;
            Variables statementvars;
            if (FromString(userdata, out keys, out statementvars))
            {
                string errlist = null;
                Variables vars = ap.functions.ExpandVars(statementvars, out errlist);

                if (errlist == null)
                {
                    int delay = vars.Exists(DelayID) ? vars[DelayID].InvariantParseInt(DefaultDelay) : (ap.VarExist(globalvarDelay) ? ap[globalvarDelay].InvariantParseInt(DefaultDelay) : DefaultDelay);
                    int updelay = vars.Exists(UpDelayID) ? vars[UpDelayID].InvariantParseInt(DefaultDelay) : (ap.VarExist(globalvarUpDelay) ? ap[globalvarUpDelay].InvariantParseInt(DefaultDelay) : DefaultDelay);
                    int shiftdelay = vars.Exists(ShiftDelayID) ? vars[ShiftDelayID].InvariantParseInt(DefaultDelay) : (ap.VarExist(globalvarShiftDelay) ? ap[globalvarShiftDelay].InvariantParseInt(DefaultDelay) : DefaultDelay);
                    string process = vars.Exists(ProcessID) ? vars[ProcessID] : (ap.VarExist(globalvarProcessID) ? ap[globalvarProcessID] : "");
                    string silentonerrors = vars.Exists(SilentOnError) ? vars[SilentOnError] : (ap.VarExist(globalvarSilentOnErrors) ? ap[globalvarSilentOnErrors] : "0");
                    string announciateonerrors = vars.Exists(AnnounciateOnError) ? vars[AnnounciateOnError] : (ap.VarExist(globalvarAnnounciateOnError) ? ap[globalvarAnnounciateOnError] : "0");

                    string res = ap.actioncontroller.ConfigFuncs.SendKeyToProcess(keys, delay, shiftdelay, updelay, process, akp);

                    if (res.HasChars())
                    {
                        if (silentonerrors.Equals("2") || (errorsreported.Contains(res) && silentonerrors.Equals("1")))
                        {
                            System.Diagnostics.Debug.WriteLine("Swallow key error " + res);
                            ap.actioncontroller.TerminateAll();
                        }
                        else
                        {
                            errorsreported.Add(res);

                            if (announciateonerrors.Equals("1"))
                            {
                                string culture = ap.VarExist(ActionSay.globalvarspeechculture) ? ap[ActionSay.globalvarspeechculture] : "Default";
                                System.IO.MemoryStream ms = ap.actioncontroller.SpeechSynthesizer.Speak("Cannot press key due to " + res, culture, "Default", 0);
                                AudioQueue.AudioSample audio = ap.actioncontroller.AudioQueueSpeech.Generate(ms);
                                ap.actioncontroller.AudioQueueSpeech.Submit(audio, 80, AudioQueue.Priority.Normal);
                            }

                            ap.ReportError(res);
                        }
                    }
                }
                else
                    ap.ReportError(errlist);
            }
            else
                ap.ReportError("Key command line not in correct format");

            return true;
        }


    }
}
