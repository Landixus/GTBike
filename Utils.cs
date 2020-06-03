// Decompiled with JetBrains decompiler
// Type: GTBikeV.Utils
// Assembly: GTBikeV, Version=0.2.3.0, Culture=neutral, PublicKeyToken=null
// MVID: 381D461B-4573-45F6-933F-1BEFED8894FA
// Assembly location: H:\Grand Theft Auto V\Scripts\GTBikeV.dll

using GTA;
using GTA.UI;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace GTBikeV
{
  public class Utils
  {
    public static float EARTH_RADIUS = 6378137f;
    public static VehicleDrivingFlags CustomDrivingStyle = (VehicleDrivingFlags) 262700;

    public static double degToRad(float angleDeg)
    {
      return (double) angleDeg * Math.PI / 180.0;
    }

    public static float radToDeg(double angleRad)
    {
      return (float) (angleRad * 180.0 / Math.PI);
    }

    public static int degToSemicircles(float angleDeg)
    {
      return (int) ((double) angleDeg / 8.38190317153931E-08);
    }

    public static PointF TravelBearing(PointF from, double brng, double distance)
    {
      if (distance == 0.0)
        return from;
      double rad1 = Utils.degToRad(from.X);
      double rad2 = Utils.degToRad(from.Y);
      double num1 = distance / (double) Utils.EARTH_RADIUS;
      double angleRad1 = Math.Asin(Math.Sin(rad1) * Math.Cos(num1) + Math.Cos(rad1) * Math.Sin(num1) * Math.Cos(brng));
      double num2 = Math.Atan2(Math.Sin(brng) * Math.Sin(num1) * Math.Cos(rad1), Math.Cos(num1) - Math.Sin(rad1) * Math.Sin(rad1));
      double angleRad2 = (rad2 + num2 + Math.PI) % (2.0 * Math.PI) - Math.PI;
      return new PointF()
      {
        X = Utils.radToDeg(angleRad1),
        Y = Utils.radToDeg(angleRad2)
      };
    }

    public static void ScreenShot(DateTime date, int sequence)
    {
      Size resolution = Screen.get_Resolution();
      using (Bitmap bitmap = new Bitmap(resolution.Width, resolution.Height))
      {
        using (Graphics graphics = Graphics.FromImage((Image) bitmap))
          graphics.CopyFromScreen(Point.Empty, Point.Empty, resolution);
        string filename = string.Format("{0}GTBikeV_{1}_{2:D2}.jpg", (object) Cyclist.UserDataPath, (object) date.ToString("yyyyMMdd_HHmmss"), (object) sequence);
        bitmap.Save(filename, ImageFormat.Jpeg);
      }
    }
  }
}
