﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WiimoteLib;
using WiiTUIO.Properties;
using System.IO;

namespace WiiTUIO.Provider
{
    public class ScreenPositionCalculator
    {

        private int minXPos;
        private int maxXPos;
        private int maxWidth;

        private int minYPos;
        private int maxYPos;
        private int maxHeight;
        private int SBPositionOffset;

        private double smoothedX, smoothedZ, smoothedRotation;
        private int orientation;

        private int leftPoint = -1;

        //private StringBuilder sb = new StringBuilder("");
        //private int count = 0;

        private CursorPos lastPos;

        private Screen primaryScreen;

        public ScreenPositionCalculator()
        {
            this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
            this.recalculateScreenBounds(this.primaryScreen);

            Settings.Default.PropertyChanged += SettingsChanged;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            lastPos = new CursorPos(0, 0, 0, 0, 0);

        }

        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "primaryMonitor")
            {
                this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
                Console.WriteLine("Setting primary monitor for screen position calculator to " + this.primaryScreen.Bounds);
                this.recalculateScreenBounds(this.primaryScreen);

          //      sb.Append("SettingsChanged\n");
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
            recalculateScreenBounds(this.primaryScreen);
        }

        private void recalculateScreenBounds(Screen screen)
        {
            Console.WriteLine("Setting primary monitor for screen position calculator to " + this.primaryScreen.Bounds);
            minXPos = -(int)(screen.Bounds.Width * Settings.Default.pointer_marginsLeftRight);
            maxXPos = screen.Bounds.Width + (int)(screen.Bounds.Width * Settings.Default.pointer_marginsLeftRight);
            maxWidth = maxXPos - minXPos;
            minYPos = -(int)(screen.Bounds.Height * Settings.Default.pointer_marginsTopBottom);
            maxYPos = screen.Bounds.Height + (int)(screen.Bounds.Height * Settings.Default.pointer_marginsTopBottom);
            maxHeight = maxYPos - minYPos;
            SBPositionOffset = (int)(screen.Bounds.Height * Settings.Default.pointer_sensorBarPosCompensation);
            //sb.Append("minXPos = " + minXPos + ";maxXPos = " + maxXPos + ";minYPos = " + minYPos + ";maxYPos = " + maxYPos + ";maxWidth = " + maxWidth + ";maxHeight = " + maxHeight+"\n");
        }

        public CursorPos CalculateCursorPos(WiimoteState wiimoteState)
        {
            int x;
            int y;

            IRState irState = wiimoteState.IRState;

            PointF relativePosition = new PointF();

            bool foundMidpoint = false;

            if (irState.IRSensors[0].Found && irState.IRSensors[1].Found)
            {
                foundMidpoint = true;

                relativePosition.X = (irState.IRSensors[0].Position.X + irState.IRSensors[1].Position.X) / 2.0f;
                relativePosition.Y = (irState.IRSensors[0].Position.Y + irState.IRSensors[1].Position.Y) / 2.0f;

                if (Settings.Default.pointer_considerRotation)
                {
                    smoothedX = smoothedX * 0.9 + wiimoteState.AccelState.RawValues.X * 0.1;
                    smoothedZ = smoothedZ * 0.9 + wiimoteState.AccelState.RawValues.Z * 0.1;

                    double absx = Math.Abs(smoothedX - 128), absz = Math.Abs(smoothedZ - 128);

                    if (orientation == 0 || orientation == 2) absx -= 5;
                    if (orientation == 1 || orientation == 3) absz -= 5;

                    if (absz >= absx)
                    {
                        if (absz > 5)
                            orientation = (smoothedZ > 128) ? 0 : 2;
                    }
                    else
                    {
                        if (absx > 5)
                            orientation = (smoothedX > 128) ? 3 : 1;
                    }

                    int l = leftPoint, r;
                    switch (orientation)
                    {
                        case 0: l = (irState.IRSensors[0].RawPosition.X < irState.IRSensors[1].RawPosition.X) ? 0 : 1; break;
                        case 1: l = (irState.IRSensors[0].RawPosition.Y > irState.IRSensors[1].RawPosition.Y) ? 0 : 1; break;
                        case 2: l = (irState.IRSensors[0].RawPosition.X > irState.IRSensors[1].RawPosition.X) ? 0 : 1; break;
                        case 3: l = (irState.IRSensors[0].RawPosition.Y < irState.IRSensors[1].RawPosition.Y) ? 0 : 1; break;
                    }
                    leftPoint = l;
                    r = 1 - l;

                    double dx = irState.IRSensors[r].RawPosition.X - irState.IRSensors[l].RawPosition.X;
                    double dy = irState.IRSensors[r].RawPosition.Y - irState.IRSensors[l].RawPosition.Y;

                    double d = Math.Sqrt(dx * dx + dy * dy);

                    dx /= d;
                    dy /= d;

                    smoothedRotation = 0.7 * smoothedRotation + 0.3 * Math.Atan2(dy, dx);
                }
            }

            if (!foundMidpoint)
            {
                CursorPos err = lastPos;
                err.OutOfReach = true;

                return err;
            }
            /*
            int offsetY = 0;

            if (Properties.Settings.Default.pointer_sensorBarPos == "top")
            {
                offsetY = -SBPositionOffset;
            }
            else if (Properties.Settings.Default.pointer_sensorBarPos == "bottom")
            {
                offsetY = SBPositionOffset;
            }*/

            relativePosition.X = 1 - relativePosition.X;
            
            if (Settings.Default.pointer_considerRotation)
            {
                relativePosition.X = relativePosition.X - 0.5F;
                relativePosition.Y = relativePosition.Y - 0.5F;

                relativePosition = this.rotatePoint(relativePosition, smoothedRotation);

                relativePosition.X = relativePosition.X + 0.5F;
                relativePosition.Y = relativePosition.Y + 0.5F;
            }

            //x = Convert.ToInt32((float)maxWidth * relativePosition.X + minXPos);
            //y = Convert.ToInt32((float)maxHeight * relativePosition.Y + minYPos) + offsetY;
            x = Convert.ToInt32((float)((relativePosition.X - Settings.Default.pointer_limit_left) / ((Settings.Default.pointer_limit_right - Settings.Default.pointer_limit_left) / primaryScreen.Bounds.Width)));
            y = Convert.ToInt32((float)((relativePosition.Y - Settings.Default.pointer_limit_top) / ((Settings.Default.pointer_limit_bottom - Settings.Default.pointer_limit_top) / primaryScreen.Bounds.Height)));
            

            if (x <= 0)
            {
                x = 0;
            }
            else if (x >= primaryScreen.Bounds.Width)
            {
                x = primaryScreen.Bounds.Width - 1;
            }
            if (y <= 0)
            {
                y = 0;
            }
            else if (y >= primaryScreen.Bounds.Height)
            {
                y = primaryScreen.Bounds.Height - 1;
            }

            CursorPos result = new CursorPos(x, y, relativePosition.X, relativePosition.Y, smoothedRotation);
            lastPos = result;

            return result;
        }

        private PointF rotatePoint(PointF point, double angle)
        {
            double sin = Math.Sin(angle);
            double cos = Math.Cos(angle);

            double xnew = point.X * cos - point.Y * sin;
            double ynew = point.X * sin + point.Y * cos;

            PointF result;

            result.X = (float)xnew;
            result.Y = (float)ynew;

            return result;
        }

    }
}
