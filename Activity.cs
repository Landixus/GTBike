// Decompiled with JetBrains decompiler
// Type: GTBikeV.Activity
// Assembly: GTBikeV, Version=0.2.3.0, Culture=neutral, PublicKeyToken=null
// MVID: 381D461B-4573-45F6-933F-1BEFED8894FA
// Assembly location: H:\Grand Theft Auto V\Scripts\GTBikeV.dll

using Dynastream.Fit;
using GTA;
using GTA.Math;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GTBikeV
{
  internal class Activity
  {
    public static PointF DEFAULT_INITIAL_GPS_POINT = new PointF(34.0617f, -118.2425f);
    private float MINIMUM_DISTANCE_TO_SAVE_ACTIVITY = 10f;
    public PointF InitialGPSPoint;
    private const int FIT_UPDATE_INTERVAL = 1000;
    private const int SCREEN_SHOT_INTERVAL_KM = 5;
    private FileIdMesg fileIdMesg;
    private DeviceInfoMesg deviceInfoMesg;
    private SessionMesg sessionMesg;
    private ActivityMesg activityMesg;
    private List<RecordMesg> records;
    private Dynastream.Fit.DateTime startTimestampUTC;
    private Dynastream.Fit.DateTime startTimestampLocal;
    private long totalMovingTime;
    private float totalDistance;
    private float initialDistance;
    private float maxSpeed;
    private int screenShotDistance;
    private bool initialized;
    private static Debug debugFitInfo;

    public System.DateTime startDateLocal { get; private set; }

    public Activity(ScriptSettings settings)
    {
      this.initialized = false;
      Activity.debugFitInfo = new Debug(new PointF(0.0f, 0.05f), new SizeF(0.16f, 0.4f), 0.4f, new SizeF(0.01f, 0.01f));
      this.fileIdMesg = new FileIdMesg();
      this.deviceInfoMesg = new DeviceInfoMesg();
      this.sessionMesg = new SessionMesg();
      this.activityMesg = new ActivityMesg();
      this.records = new List<RecordMesg>();
      this.fileIdMesg.SetType(new Dynastream.Fit.File?(Dynastream.Fit.File.Activity));
      this.fileIdMesg.SetManufacturer(new ushort?((ushort) 1));
      this.fileIdMesg.SetProduct(new ushort?((ushort) 1));
      this.fileIdMesg.SetSerialNumber(new uint?(12345U));
      this.startDateLocal = System.DateTime.Now;
      this.startTimestampUTC = new Dynastream.Fit.DateTime(System.DateTime.UtcNow);
      this.startTimestampLocal = new Dynastream.Fit.DateTime(System.DateTime.Now);
      this.fileIdMesg.SetTimeCreated(this.startTimestampUTC);
      this.deviceInfoMesg.SetProductName("GTBike V");
      this.deviceInfoMesg.SetSoftwareVersion(new float?(1f));
      this.deviceInfoMesg.SetTimestamp(this.startTimestampUTC);
      this.deviceInfoMesg.SetManufacturer(new ushort?((ushort) 1));
      this.totalDistance = 0.0f;
      this.initialDistance = -1f;
      this.totalMovingTime = 0L;
      this.InitialGPSPoint = new PointF((float) settings.GetValue<float>("main", "InitialGPSPointLat", (M0) (double) Activity.DEFAULT_INITIAL_GPS_POINT.X), (float) settings.GetValue<float>("main", "InitialGPSPointLong", (M0) (double) Activity.DEFAULT_INITIAL_GPS_POINT.Y));
      Timer.Add("FIT_ACTIVITY", 1000U);
      this.screenShotDistance = 5;
      this.initialized = true;
    }

    public void Update(
      Vector3 position,
      float distance,
      float speed,
      int cadence,
      int power,
      float elevation,
      float slope)
    {
      if (Timer.timedOut("FIT_ACTIVITY") && this.initialized)
      {
        PointF position1 = new PointF((float) position.X, (float) position.Y);
        if ((double) this.initialDistance < 0.0)
          this.initialDistance = distance;
        this.addRecord(position1, distance - this.initialDistance, speed, cadence, power, elevation, slope);
        this.totalDistance = distance - this.initialDistance;
        if ((double) speed > 0.0)
        {
          this.totalMovingTime += 1000L;
          if ((double) speed > (double) this.maxSpeed)
            this.maxSpeed = speed;
        }
      }
      Activity.debugFitInfo.Draw();
    }

    private void addRecord(
      PointF position,
      float distance,
      float speed,
      int cadence,
      int power,
      float groundElevation,
      float slope)
    {
      Activity.debugFitInfo.DebugString = "Adding Record\n";
      RecordMesg recordMesg = new RecordMesg();
      recordMesg.SetTimestamp(new Dynastream.Fit.DateTime(System.DateTime.UtcNow));
      recordMesg.SetCadence(new byte?((byte) cadence));
      recordMesg.SetPower(new ushort?((ushort) power));
      recordMesg.SetSpeed(new float?(speed));
      recordMesg.SetDistance(new float?(distance));
      double num = -1.0 * System.Math.Atan2((double) position.Y, (double) position.X) + System.Math.PI / 2.0;
      double distance1 = System.Math.Sqrt((double) position.Y * (double) position.Y + (double) position.X * (double) position.X);
      PointF pointF = Utils.TravelBearing(this.InitialGPSPoint, num, distance1);
      recordMesg.SetPositionLat(new int?(Utils.degToSemicircles(pointF.X)));
      recordMesg.SetPositionLong(new int?(Utils.degToSemicircles(pointF.Y)));
      recordMesg.SetAltitude(new float?(groundElevation));
      recordMesg.SetGrade(new float?(slope));
      Activity.debugFitInfo.DebugString += string.Format("speed={0} distance={1}\n", (object) speed, (object) distance);
      Activity.debugFitInfo.DebugString += string.Format("timestamp={0}\n", (object) recordMesg.GetTimestamp().GetTimeStamp());
      Activity.debugFitInfo.DebugString += string.Format("Coords X={0} Y={1}\n", (object) position.X, (object) position.Y);
      Activity.debugFitInfo.DebugString += string.Format("bearing={0}\ndeg={1}\n", (object) num, (object) Utils.radToDeg(num));
      Activity.debugFitInfo.DebugString += string.Format("GPS Lat={0} Y={1}\n", (object) pointF.X, (object) pointF.Y);
      Activity.debugFitInfo.DebugString += string.Format("Total distance={0}\n", (object) this.totalDistance);
      Activity.debugFitInfo.DebugString += string.Format("Start timestamp {0}\n", (object) this.startTimestampUTC.GetTimeStamp());
      Activity.debugFitInfo.DebugString += string.Format("Start local ts  {0}\n", (object) this.startTimestampLocal.GetTimeStamp());
      this.records.Add(recordMesg);
      if ((double) distance <= (double) (this.screenShotDistance * 1000))
        return;
      Utils.ScreenShot(this.startDateLocal, this.screenShotDistance);
      this.screenShotDistance += 5;
    }

    public void Encode(float distance = 0.0f)
    {
      if ((double) distance != 0.0)
        this.totalDistance = distance;
      if ((double) this.totalDistance <= (double) this.MINIMUM_DISTANCE_TO_SAVE_ACTIVITY)
        return;
      Dynastream.Fit.Encode encode = new Dynastream.Fit.Encode(ProtocolVersion.V20);
      FileStream fileStream = new FileStream(string.Format("{0}GTBikeV_{1}.fit", (object) Cyclist.UserDataPath, (object) this.startDateLocal.ToString("yyyyMMdd_HHmmss")), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
      this.activityMesg.SetType(new Dynastream.Fit.Activity?(Dynastream.Fit.Activity.Manual));
      this.activityMesg.SetNumSessions(new ushort?((ushort) 1));
      this.activityMesg.SetTimestamp(this.startTimestampUTC);
      this.activityMesg.SetLocalTimestamp(new uint?(this.startTimestampLocal.GetTimeStamp()));
      this.activityMesg.SetTotalTimerTime(new float?((float) this.totalMovingTime / 1000f));
      this.activityMesg.SetEvent(new Event?(Event.Activity));
      this.activityMesg.SetEventType(new EventType?(EventType.Stop));
      int num = (int) new Dynastream.Fit.DateTime(System.DateTime.UtcNow).GetTimeStamp() - (int) this.startTimestampUTC.GetTimeStamp();
      this.sessionMesg.SetTimestamp(this.startTimestampUTC);
      this.sessionMesg.SetStartTime(this.startTimestampUTC);
      this.sessionMesg.SetSport(new Sport?(Sport.Cycling));
      this.sessionMesg.SetSubSport(new SubSport?(SubSport.IndoorCycling));
      this.sessionMesg.SetTotalElapsedTime(new float?((float) num));
      this.sessionMesg.SetTotalTimerTime(new float?((float) this.totalMovingTime / 1000f));
      this.sessionMesg.SetTotalDistance(new float?(this.totalDistance));
      this.sessionMesg.SetAvgSpeed(new float?(this.totalDistance / ((float) this.totalMovingTime / 1000f)));
      this.sessionMesg.SetMaxSpeed(new float?(this.maxSpeed));
      this.sessionMesg.SetEvent(new Event?(Event.Session));
      this.sessionMesg.SetEventType(new EventType?(EventType.Stop));
      encode.Open((Stream) fileStream);
      encode.Write((Mesg) this.fileIdMesg);
      encode.Write((Mesg) this.deviceInfoMesg);
      encode.Write((IEnumerable<Mesg>) this.records);
      encode.Write((Mesg) this.sessionMesg);
      encode.Write((Mesg) this.activityMesg);
      encode.Close();
      fileStream.Close();
    }
  }
}
