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
    abstract class AbstractWindow
    {
        public event WindowResizedHandler onResized;

        public event WindowVisibleToggledHandler onVisibleToggled;

        private bool hidden_;
        public bool hidden
        {
            get
            {
                return hidden_;
            }

            set
            {
                if (value != hidden_)
                {
                    hidden_ = value;
                    if (onVisibleToggled != null)
                    {
                        onVisibleToggled(new WindowVisibleToggledEvent(!value));
                    }
                }
            }
        }

        public Rect rect;
        public string title = "";
        public bool resizable = true;
        public bool draggable = true;
        public readonly int id = UnityEngine.Random.Range(1000, 2000000);

        private bool resizeHandleMouseDown;
        private Vector3 mouseDownPos;
        private Rect resizeOrigRect;
        private readonly IRCConfig config;
        private readonly string configName;

        protected AbstractWindow(string configName, IRCConfig config, Rect defaultRect)
        {
            this.configName = configName;
            this.config = config;

            if (!config.GetWindowRect(configName, ref rect))
            {
                rect = defaultRect;
            }
        }

        public virtual void draw()
        {
            if (!hidden)
            {
                rect = GUILayout.Window(id, rect, drawContents, title);
            }
        }

        private void drawContents(int id)
        {
            drawContents();

            bool inResizeHandle = false;
            if (resizable)
            {
                inResizeHandle = resizeWindow();
            }
            if (draggable && !inResizeHandle)
            {
                GUI.DragWindow();
            }
        }

        protected abstract void drawContents();

        private bool resizeWindow()
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            Rect windowHandle = new Rect(rect.x + rect.width - 8, rect.y + rect.height - 8, 8, 8);
            if (windowHandle.Contains(mousePos))
            {
                Texture2D cursorTex = GameDatabase.Instance.GetTexture("KSPIRC/resize-cursor", false);
                Cursor.SetCursor(cursorTex, new Vector2(7, 7), CursorMode.ForceSoftware);
                if (!resizeHandleMouseDown && Input.GetMouseButtonDown(0))
                {
                    resizeHandleMouseDown = true;
                    mouseDownPos = mousePos;
                    resizeOrigRect = rect;
                }
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }

            if (resizeHandleMouseDown)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    resizeHandleMouseDown = false;

                    UpdateConfig();
                    config.Save();
                }
                else
                {
                    rect.width = Mathf.Clamp(resizeOrigRect.width + (mousePos.x - mouseDownPos.x), 50, Screen.width);
                    rect.height = Mathf.Clamp(resizeOrigRect.height + (mousePos.y - mouseDownPos.y), 50, Screen.height);
                    if (onResized != null)
                    {
                        onResized();
                    }
                }
            }

            return resizeHandleMouseDown;
        }

        internal void UpdateConfig()
        {
            config.SetWindowRect(configName, rect);
        }

        public virtual void update()
        {
        }
    }

    delegate void WindowResizedHandler();
    delegate void WindowVisibleToggledHandler(WindowVisibleToggledEvent e);

    class WindowVisibleToggledEvent : EventArgs
    {
        public readonly bool visible;

        public WindowVisibleToggledEvent(bool visible)
        {
            this.visible = visible;
        }
    }
}