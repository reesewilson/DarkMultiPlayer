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
    class IRCChatWindow : AbstractWindow
    {
        private IRCConfig config_;
        private IRCConfig config
        {
            get
            {
                return config_;
            }

            set
            {
                config_ = value;

                foreach (ChannelGUI channelGUI in channelGUIs.Values)
                {
                    channelGUI.config = config;
                }
            }
        }

        public bool anyChannelsHighlightedPrivateMessage
        {
            get
            {
                return channelGUIs.Values.Any(gui => gui.channelHighlightedPrivateMessage);
            }
        }
        public bool anyChannelsHighlightedMessage
        {
            get
            {
                return channelGUIs.Values.Any(gui => gui.channelHighlightedMessage);
            }
        }
        public bool anyChannelsHighlightedJoin
        {
            get
            {
                return channelGUIs.Values.Any(gui => gui.channelHighlightedJoin);
            }
        }

        public event ChannelClosedHandler channelClosedEvent;
        public event UserCommandHandler onUserCommandEntered;
        public event ShowConfigHandler onShowConfigHandler;

        private bool namesHidden_;
        private bool namesHidden
        {
            get
            {
                return namesHidden_;
            }

            set
            {
                namesHidden_ = value;
                foreach (ChannelGUI channelGUI in channelGUIs.Values)
                {
                    channelGUI.namesHidden = value;
                }
            }
        }

        private Dictionary<string, ChannelGUI> channelGUIs = new Dictionary<string, ChannelGUI>();
        private List<string> handles = new List<string>();
        private ChannelGUI currentChannelGUI;
        private readonly IRCLinkWindow linkWindow;
        private bool stylesInitialized;
        private GUIStyle buttonActiveStyle;
        private GUIStyle buttonHighlightedNicknameStyle;
        private GUIStyle buttonHighlightedStyle;

        public IRCChatWindow(IRCLinkWindow linkWindow, string version, IRCConfig config)
            : base("chat", config, new Rect(Screen.width / 6, Screen.height / 6, Screen.width * 2 / 3, Screen.height * 2 / 3))
        {
            this.linkWindow = linkWindow;
            this.config = config;

            hidden = true;
            title = "IRC - " + version + " - " + (config.twitch ? "Twitch" : config.host + ":" + config.port);

            onResized += windowResized;
            onVisibleToggled += (e) => windowVisibleToggled(e.visible);
        }

        protected override void drawContents()
        {
            initStyles();

            GUILayout.BeginVertical();
            drawButtons();

            if (currentChannelGUI != null)
            {
                currentChannelGUI.draw(this);
            }

            GUILayout.EndVertical();
        }

        private void initStyles()
        {
            if (!stylesInitialized)
            {
                buttonActiveStyle = new GUIStyle(GUI.skin.button);
                buttonActiveStyle.fontStyle = FontStyle.Bold;

                buttonHighlightedNicknameStyle = new GUIStyle(GUI.skin.button);
                buttonHighlightedNicknameStyle.normal.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.onHover.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.hover.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.onActive.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.active.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.onFocused.textColor = Color.yellow;
                buttonHighlightedNicknameStyle.focused.textColor = Color.yellow;

                buttonHighlightedStyle = new GUIStyle(GUI.skin.button);
                buttonHighlightedStyle.normal.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.onHover.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.hover.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.onActive.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.active.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.onFocused.textColor = XKCDColors.BlueGrey;
                buttonHighlightedStyle.focused.textColor = XKCDColors.BlueGrey;

                stylesInitialized = true;
            }
        }

        private void windowResized()
        {
            foreach (ChannelGUI channelGUI in channelGUIs.Values)
            {
                channelGUI.windowResized();
            }
        }

        private void windowVisibleToggled(bool visible)
        {
            if (visible)
            {
                if (currentChannelGUI != null)
                {
                    currentChannelGUI.hidden = false;
                } 
            }
            else
            {
                foreach (ChannelGUI channelGUI in channelGUIs.Values)
                {
                    channelGUI.hidden = true;
                }

                if (linkWindow != null)
                {
                    linkWindow.hidden = true;
                }
            }
        }


        private void drawButtons()
        {
            GUILayout.BeginHorizontal();
            for (int ix = 0; ix < handles.Count; ix++)
            {
                string handle = handles[ix];

                ChannelGUI channelGUI = getChannelGUI(handle);
                GUIStyle buttonStyle;
                if (channelGUI.Equals(currentChannelGUI))
                {
                    buttonStyle = buttonActiveStyle;
                }
                else if (channelGUI.channelHighlightedPrivateMessage)
                {
                    buttonStyle = buttonHighlightedNicknameStyle;
                }
                else if (channelGUI.channelHighlightedMessage)
                {
                    buttonStyle = buttonHighlightedStyle;
                }
                else
                {
                    buttonStyle = GUI.skin.button;
                }

                if (GUILayout.Button(handle, buttonStyle))
                {
                    currentChannelGUI = channelGUI;
                    currentChannelGUI.hidden = false;
                    foreach (ChannelGUI gui in channelGUIs.Values.Where(gui => !gui.Equals(currentChannelGUI)))
                    {
                        gui.hidden = true;
                    }
                }
            }

            GUILayout.FlexibleSpace();

            if ((currentChannelGUI != null) && (channelGUIs.Count > 1))
            {
                if (currentChannelGUI.handle.StartsWith("#"))
                {
                    if (GUILayout.Button(namesHidden ? "<" : ">"))
                    {
                        namesHidden = !namesHidden;
                    }
                }
                if (GUILayout.Button("X"))
                {
                    closeChannel(currentChannelGUI.handle);
                }
            }

            if (GUILayout.Button("Config"))
            {
                if (onShowConfigHandler != null)
                {
                    onShowConfigHandler(new ShowConfigEvent());
                }
            }
            GUILayout.EndHorizontal();
        }

        private void closeChannel(string handle)
        {
            if (channelClosedEvent != null)
            {
                channelClosedEvent(new ChannelEvent(handle));
            }
            channelGUIs.Remove(handle);
            handles.Remove(handle);
            currentChannelGUI = channelGUIs.Values.FirstOrDefault();
        }

        internal class TextInputState
        {
            internal TextInputState(bool focused, int lastCursorPos, int lastSelectCursorPos)
            {
                this.focused = focused;
                this.lastCursorPos = lastCursorPos;
                this.lastSelectCursorPos = lastSelectCursorPos;
            }

            internal readonly bool focused;
            internal readonly int lastCursorPos;  //set cursor position
            internal readonly int lastSelectCursorPos;  //set selection cursor position
        }

        public void addToChannel(string handle, string sender, string text, IRCCommand cmd = null)
        {
            TextInputState textInputState = GetInputState();
            ChannelGUI channelGUI = getChannelGUI(handle);
            channelGUI.addToBuffer(sender, text, cmd);

            // show this channel if no channel is visible yet
            if (currentChannelGUI == null)
            {
                currentChannelGUI = channelGUI;
                currentChannelGUI.hidden = false;
            }

            RestoreInputFocus(textInputState);
        }

        public void addChannelNames(string handle, string[] names)
        {
            getChannelGUI(handle).addNames(names);
        }

        public void addSingleChannelName(string handle, string name)
        {
            getChannelGUI(handle).addSingleName(name);
        }

        public void endOfChannelNames(string handle)
        {
            getChannelGUI(handle).endOfNames();
        }

        public void removeChannelName(string handle, string name)
        {
            getChannelGUI(handle).removeName(name);
        }

        public void renameInChannel(string handle, string oldName, string newName)
        {
            getChannelGUI(handle).rename(oldName, newName);
        }

        public void changeUserModeInChannel(string handle, string name, string mode)
        {
            getChannelGUI(handle).changeUserMode(name, mode);
        }

        public string[] getChannelsContainingName(string name)
        {
            List<string> handles = new List<string>();
            foreach (string handle in channelGUIs.Keys)
            {
                if (getChannelGUI(handle).containsName(name))
                {
                    handles.Add(handle);
                }
            }
            return handles.ToArray();
        }

        public void setChannelTopic(string handle, string topic)
        {
            getChannelGUI(handle).topic = topic;
        }

        public string getCurrentChannelName()
        {
            return (currentChannelGUI != null) ? currentChannelGUI.handle : null;
        }

        private ChannelGUI getChannelGUI(string handle)
        {
            ChannelGUI channelGUI;
            if (!channelGUIs.ContainsKey(handle))
            {
                channelGUI = new ChannelGUI(linkWindow, handle, config);
                channelGUI.hidden = true;
                channelGUI.onUserCommandEntered += (e) => userCommandEntered(e.command);
                channelGUIs.Add(handle, channelGUI);
                handles.Add(handle);
                handles.Sort(StringComparer.CurrentCultureIgnoreCase);
            }
            else
            {
                channelGUI = channelGUIs[handle];
            }
            return channelGUI;
        }

        internal TextInputState GetInputState()
        {
            bool focused = (GUI.GetNameOfFocusedControl() == ChannelGUI.INPUT_CONTROL_NAME);
            int lastCursorPos = -1;
            int lastSelectCursorPos = -1;

            if (focused)
            {
                TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (te != null)
                {
                    lastCursorPos = te.cursorIndex;  //set cursor position
                    lastSelectCursorPos = te.selectIndex;  //set selection cursor position
                }
            }

            return new TextInputState(focused, lastCursorPos, lastSelectCursorPos);
        }


        internal void RestoreInputFocus(TextInputState state)
        {
            if (state.focused)
            {
                GUI.FocusControl(ChannelGUI.INPUT_CONTROL_NAME);
                TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (te != null)
                {
                    //these two lines prevent a "select all" effect on the textfield which seems to be the default GUI.FocusControl behavior
                    te.cursorIndex = state.lastCursorPos;  //set cursor position
                    te.selectIndex = state.lastSelectCursorPos;  //set selection cursor position
                }
            }
        }

        private void userCommandEntered(UserCommand cmd)
        {
            if (onUserCommandEntered != null)
            {
                onUserCommandEntered(new UserCommandEvent(cmd));
            }
        }
    }

    delegate void ChannelClosedHandler(ChannelEvent e);

    class ChannelEvent : EventArgs
    {
        public string handle
        {
            get;
            private set;
        }

        public ChannelEvent(string handle)
        {
            this.handle = handle;
        }
    }
}