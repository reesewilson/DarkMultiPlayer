/*
KSPIRC - Internet Relay Chat plugin for Kerbal Space Program.
Copyright (C) 2013 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPIRC
{
    class IRCConfigWindow : AbstractWindow
    {
        private bool stylesInitialized;
        private readonly IRCConfig config;
        private GUIStyle buttonActiveStyle;

        private Boolean wipTwitch;
        private string wipHost;
        private string wipPort;
        private bool wipSecure;
        private string wipNick;
        private string wipUser;
        private string wipServerPassword;
        private bool wipForceSimpleRender;
        private string wipChannels;
        private bool wipTTS;
        private int wipTTSVolume;
        private bool wipDebug;

        public event ConfigChangedHandler configChangedEvent;

        public IRCConfigWindow(string version, IRCConfig config)
            : base("config", config, new Rect(Screen.width / 6, Screen.height / 6, 300, 200))
        {
            this.config = config;
            this.wipTwitch = config.twitch;
            this.wipHost = (config.host == null ? "" : config.host);
            this.wipPort = Convert.ToString(config.port);
            this.wipSecure = config.secure;
            this.wipNick = (config.nick == null ? "" : config.nick);
            this.wipUser = (config.user == null ? "" : config.user);
            this.wipServerPassword = (config.serverPassword == null ? "" : config.serverPassword);
            this.wipForceSimpleRender = config.forceSimpleRender;
            this.wipChannels = config.channels;
            this.wipTTS = config.tts;
            this.wipTTSVolume = config.ttsVolume;
            this.wipDebug = config.debug;


            title = "DMP IRC Config - " + version;
        }

        protected override void drawContents()
        {
            initStyles();

            GUILayout.BeginVertical();
            wipTwitch = doConfigBool("Twitch Channel", wipTwitch);
            if (!wipTwitch)
            {
                wipHost = doConfigText("Host", wipHost);
                wipPort = doConfigText("Port", wipPort);
                wipSecure = doConfigBool("Secure", wipSecure);
            }

            wipNick = doConfigText("Nick", wipNick);
            wipUser = doConfigText("User", wipUser);
            wipServerPassword = doConfigPassword("Password", wipServerPassword);
            wipForceSimpleRender = doConfigBool("Force Simple Render", wipForceSimpleRender);
            wipChannels = doConfigText("Auto Join Channels", wipChannels);
            wipTTS = doConfigBool("Text-to-Speech Is a WIP and will be coming soon!", wipTTS);
            wipDebug = doConfigBool("Debug", wipDebug);

            if (GUILayout.Button("Confirm"))
            {
                config.twitch = wipTwitch;
                config.host = wipHost;
                config.port = Convert.ToInt32(wipPort);
                config.secure = wipSecure;
                config.nick = wipNick;
                config.user = wipUser;
                config.serverPassword = wipServerPassword;
                config.forceSimpleRender = wipForceSimpleRender;
                config.channels = wipChannels;
                config.tts = wipTTS;
                config.ttsVolume = wipTTSVolume;
                config.debug = wipDebug;

                if (configChangedEvent != null)
                {
                    configChangedEvent(new ConfigChangedEvent(config));
                }
            }
            GUILayout.EndVertical();

            if (GUI.Button(new Rect(rect.width - 18, 2, 16, 16), ""))
            {
                hidden = true;
            }
        }

        private bool doConfigBool(string label, bool content)
        {
            GUILayout.BeginHorizontal();
            //GUILayout.Label(label);
            bool result = GUILayout.Toggle(content, label);
            GUILayout.EndHorizontal();

            return result;
        }

        private int doConfigRangedInt(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            int result = (int)Math.Round(GUILayout.HorizontalSlider(value, min, max));
            GUILayout.EndHorizontal();

            return result;
        }
        private string doConfigText(string label, string content)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            string result = GUILayout.TextField(content);
            GUILayout.EndHorizontal();

            return result;
        }

        private string doConfigPassword(string label, string content)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            string result = GUILayout.PasswordField(content, '*');
            GUILayout.EndHorizontal();

            return result;
        }

        private void initStyles()
        {
            if (!stylesInitialized)
            {
                buttonActiveStyle = new GUIStyle(GUI.skin.button);
                buttonActiveStyle.fontStyle = FontStyle.Bold;

                stylesInitialized = true;
            }
        }
    }
}