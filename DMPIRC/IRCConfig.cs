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
    class IRCConfig : IPersistenceLoad, IPersistenceSave
    {

        [Persistent]
        internal string host = "irc.esper.net";

        [Persistent]
        internal int port = 5555;

        [Persistent]
        internal bool secure = false;

        [Persistent]
        internal bool twitch = false;

        [Persistent]
        internal string user = null;

        [Persistent]
        internal string serverPassword = null;

        [Persistent]
        internal string nick = "";

        [Persistent]
        internal bool forceSimpleRender = false;

        [Persistent]
        internal string channels = "";

        [Persistent]
        internal bool tts = false;

        [Persistent]
        internal int ttsVolume = 100;

        [Persistent]
        internal bool debug = false;

        [Persistent]
        internal Dictionary<string, Rect> windowRects = new Dictionary<string, Rect>();

        private string settingsFile = KSPUtil.ApplicationRootPath + "GameData/KSPIRC/irc.cfg";
            

        public IRCConfig()
        {
            ConfigNode settingsConfigNode = ConfigNode.Load(settingsFile) ?? new ConfigNode();
            ConfigNode.LoadObjectFromConfig(this, settingsConfigNode);

            for (int ix = 0; ix < settingsConfigNode.values.Count; ix++)
            {
                ConfigNode.Value node = settingsConfigNode.values[ix];

                if (node.name.StartsWith("rect-"))
                {
                    string[] elements = node.value.Split(',');
                    if (elements.Length == 4)
                    {
                        string name = node.name.Substring("rect-".Length);

                        float left = float.Parse(elements[0]);
                        float top = float.Parse(elements[1]);
                        float width = float.Parse(elements[2]);
                        float height = float.Parse(elements[3]);

                        Rect rectValue = new Rect(left, top, width, height);

                        this.windowRects[name] = rectValue;
                    }
                }
            }
        }

        public void Save()
        {
            ConfigNode cnSaveWrapper = ConfigNode.CreateConfigFromObject(this);


            foreach (string name in windowRects.Keys)
            {
                Rect rect = windowRects[name];

                string value = rect.xMin + "," + rect.yMin + "," + rect.width + "," + rect.height;

                cnSaveWrapper.AddValue("rect-" + name, value);
            }

            cnSaveWrapper.Save(settingsFile);
        }

        public void SetWindowRect(string name, Rect value)
        {
            windowRects[name] = value;
        }

        public bool GetWindowRect(string name, ref Rect destination)
        {
            if (windowRects.TryGetValue(name, out destination))
            {
                return true;
            }

            return false;
        }

        #region Interface Methods
        /// <summary>
        /// Wrapper for our overridable functions
        /// </summary>
        void IPersistenceLoad.PersistenceLoad()
        {
            OnDecodeFromConfigNode();
        }
        /// <summary>
        /// Wrapper for our overridable functions
        /// </summary>
        void IPersistenceSave.PersistenceSave()
        {
            OnEncodeToConfigNode();
        }   
        
        /// <summary>
        /// This overridable function executes whenever the object is loaded from a config node structure. Use this for complex classes that need decoding from simple confignode values
        /// </summary>
        public virtual void OnDecodeFromConfigNode() { }
        /// <summary>
        /// This overridable function executes whenever the object is encoded to a config node structure. Use this for complex classes that need encoding into simple confignode values
        /// </summary>
        public virtual void OnEncodeToConfigNode() { }
        #endregion
    }
}
