// Decompiled with JetBrains decompiler
// Type: GTBikeV.Cyclist
// Assembly: GTBikeV, Version=0.2.3.0, Culture=neutral, PublicKeyToken=null
// MVID: 381D461B-4573-45F6-933F-1BEFED8894FA
// Assembly location: H:\Grand Theft Auto V\Scripts\GTBikeV.dll

using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Microsoft.CSharp.RuntimeBinder;
using NativeUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace GTBikeV
{
  public class Cyclist : Script
  {
    private const float SMOOTH_FACTOR = 0.01f;
    private const float SLOPE_CALCULATION_DISTANCE = 2f;
    private const int MAX_DRAFT_DISTANCE = 10;
    private const int DRIVE_UPDATE_INTERVAL = 1000;
    private const float DEFAULT_SLOPE_SCALE = 0.5f;
    private const float DEFAULT_USER_WEIGHT = 75f;
    private const float MAX_USER_WEIGHT = 150f;
    private const float MIN_USER_WEIGHT = 40f;
    public static string UserDataPath;
    public static string UserSettingsPath;
    private bool bikeSpawn;
    private Dictionary<string, Cyclist.Bicycle> availableBicycles;
    private HUD.HUDData hudData;
    private float smoothedSlope;
    private bool autoPilot;
    private float visualSpeed;
    private bool lastAutoPilotValue;
    private Activity currentActivity;
    private Course currentCourse;
    private MenuPool menuPool;
    private UIMenuItem activateModMenuItem;
    private UIMenuCheckboxItem autodriveMenuItem;
    private UIMenuListItem courseListMenuItem;
    private UIMenuSliderItem weightMenuItem;
    private UIMenuItem saveActivityMenuItem;
    private bool userWeightChanged;
    private VehicleDrivingFlags drivingStyle;
    private float moveLeftRight;
    private int cameraHeading;
    private int finalCameraHeading;
    private int nextCameraChangeTicks;
    private bool waypointPositionChanged;
    private int speedToZeroWarningHandle;
    private ScriptSettings settings;
    private float slopeScale;
    private Cyclist.Bicycle selectedBike;
    private float userWeight;
    private Debug debugGenInfo;
    private object multipurposeValue;

    public Vehicle CurrentVehicle { get; private set; }

    private Vehicle LastVehicle { get; set; }

    public Player CurrentPlayer { get; private set; }

    public Cyclist()
    {
      base.\u002Ector();
      this.currentActivity = (Activity) null;
      this.currentCourse = (Course) null;
      this.bikeSpawn = false;
      this.hudData = new HUD.HUDData();
      this.availableBicycles = new Dictionary<string, Cyclist.Bicycle>()
      {
        {
          "SCORCHER",
          new Cyclist.Bicycle(new Model("SCORCHER"), 12.5f, 0.57f)
        },
        {
          "TRIBIKE",
          new Cyclist.Bicycle(new Model("TRIBIKE"), 8f, 0.36f)
        },
        {
          "TRIBIKE2",
          new Cyclist.Bicycle(new Model("TRIBIKE2"), 8.5f, 0.36f)
        },
        {
          "TRIBIKE3",
          new Cyclist.Bicycle(new Model("TRIBIKE3"), 6.9f, 0.36f)
        },
        {
          "FIXTER",
          new Cyclist.Bicycle(new Model("FIXTER"), 9f, 0.4f)
        },
        {
          "CRUISER",
          new Cyclist.Bicycle(new Model("CRUISER"), 15f, 0.55f)
        },
        {
          "BMX",
          new Cyclist.Bicycle(new Model("BMX"), 5f, 0.6f)
        }
      };
      this.readConfig();
      this.createMenus();
      Timer.Add("DRIVE_UPDATE", 1000U);
      this.add_Tick(new EventHandler(this.onTick));
      this.add_KeyUp(new KeyEventHandler(this.onKeyUp));
      this.add_KeyDown(new KeyEventHandler(this.onKeyDown));
      this.add_Aborted(new EventHandler(this.onAbort));
      this.set_Interval(10);
    }

    private void readConfig()
    {
      CultureInfo.CurrentCulture = new CultureInfo("", false);
      Cyclist.UserDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Rockstar Games\\GTA V\\";
      Cyclist.UserSettingsPath = string.Format("{0}ModSettings\\", (object) Cyclist.UserDataPath);
      string path = string.Format("{0}GTBikeVConfig.ini", (object) Cyclist.UserSettingsPath);
      this.settings = ScriptSettings.Load(path);
      if (!File.Exists(path))
      {
        this.settings.SetValue<string>("main", "SelectedBike", (M0) "TRIBIKE");
        this.settings.SetValue<float>("main", "SlopeScale", (M0) 0.5);
        this.settings.SetValue<bool>("main", "DebugWindow", (M0) 0);
        this.settings.SetValue<float>("main", "InitialGPSPointLat", (M0) (double) Activity.DEFAULT_INITIAL_GPS_POINT.X);
        this.settings.SetValue<float>("main", "InitialGPSPointLong", (M0) (double) Activity.DEFAULT_INITIAL_GPS_POINT.Y);
        this.settings.SetValue<ushort>("main", "FECDeviceId", (M0) 0);
        this.settings.SetValue<ushort>("main", "ControlsDeviceId", (M0) 12345);
        this.settings.SetValue<bool>("main", "Imperial", (M0) 0);
        this.settings.SetValue<float>("main", "UserWeightKg", (M0) 75.0);
      }
      this.userWeight = (float) this.settings.GetValue<float>("main", "UserWeightKg", (M0) 75.0);
      if ((double) this.userWeight < 40.0 || (double) this.userWeight > 150.0)
        this.userWeight = 75f;
      this.selectedBike = this.availableBicycles[(string) this.settings.GetValue<string>("main", "SelectedBike", (M0) "TRIBIKE")];
      this.slopeScale = (float) this.settings.GetValue<float>("main", "SlopeScale", (M0) 0.5);
      this.debugGenInfo = new Debug(new PointF(0.2f, 0.3f), new SizeF(0.6f, 0.4f), 0.5f, new SizeF(0.01f, 0.01f));
      if (this.settings.GetValue<bool>("main", "DebugWindow", (M0) 0) != null)
        Debug.Show();
      else
        Debug.Hide();
      HUD.SetImperialSystem((bool) this.settings.GetValue<bool>("main", "Imperial", (M0) 0));
      this.settings.Save();
    }

    private void onTick(object sender, EventArgs e)
    {
      this.CurrentPlayer = Game.get_Player();
      this.CurrentVehicle = this.CurrentPlayer.get_Character().get_CurrentVehicle();
      this.CurrentPlayer.set_IsInvincible(true);
      this.CurrentPlayer.set_WantedLevel(0);
      this.debugGenInfo.DebugString = "";
      if (Entity.op_Inequality((Entity) this.CurrentVehicle, (Entity) null) && this.CurrentPlayer.get_IsPlaying() && (this.CurrentPlayer.get_CanControlCharacter() && DataTransceiver.IsOK))
      {
        this.LastVehicle = this.CurrentVehicle;
        if (!this.CurrentPlayer.get_IsDead())
        {
          if (Function.Call<bool>((Hash) 4074147724792802446L, new InputArgument[2]
          {
            InputArgument.op_Implicit(this.CurrentPlayer.get_Handle()),
            InputArgument.op_Implicit(true)
          }) == null)
          {
            this.visualSpeed = (double) DataTransceiver.Speed_ms < 0.0 ? 0.0f : DataTransceiver.Speed_ms;
            int num1 = DataTransceiver.Cadence_rpm < 0 ? 0 : DataTransceiver.Cadence_rpm;
            if (((Entity) this.CurrentVehicle).get_IsInAir())
              Function.Call((Hash) 1801159460433909150L, new InputArgument[9]
              {
                Entity.op_Implicit((Entity) this.CurrentVehicle),
                InputArgument.op_Implicit(1),
                InputArgument.op_Implicit(0),
                InputArgument.op_Implicit(0),
                InputArgument.op_Implicit(-10f),
                InputArgument.op_Implicit(false),
                InputArgument.op_Implicit(true),
                InputArgument.op_Implicit(true),
                InputArgument.op_Implicit(false)
              });
            if (((double) this.visualSpeed <= 0.0 || !this.autoPilot) && (SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskControlVehicle) || SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskCarDriveWander)))
              this.CurrentPlayer.get_Character().get_Task().ClearAll();
            if ((double) this.visualSpeed > 0.0 && Timer.timedOut("DRIVE_UPDATE"))
            {
              if (this.currentCourse != null)
              {
                if (this.currentCourse.IsCloseToCurrentWaypoint(((Entity) this.CurrentVehicle).get_Position()))
                {
                  this.currentCourse.GoNextWaypoint();
                  this.waypointPositionChanged = true;
                }
                if (this.autoPilot && (!SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskControlVehicle) || this.waypointPositionChanged || this.lastAutoPilotValue != this.autoPilot))
                {
                  this.CurrentPlayer.get_Character().get_Task().DriveTo(this.CurrentVehicle, World.get_WaypointPosition(), this.currentCourse.WaypointRadius / 2f, this.visualSpeed, (DrivingStyle) this.drivingStyle);
                  this.lastAutoPilotValue = this.autoPilot;
                  this.waypointPositionChanged = false;
                }
              }
              else if (this.autoPilot && !SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskCarDriveWander))
                this.CurrentPlayer.get_Character().get_Task().CruiseWithVehicle(this.CurrentVehicle, this.visualSpeed, (DrivingStyle) this.drivingStyle);
              if (this.autoPilot)
              {
                Function.Call((Hash) -5650329060376590909L, new InputArgument[1]
                {
                  InputArgument.op_Implicit(10)
                });
                Function.Call((Hash) -6399063077906526660L, new InputArgument[1]
                {
                  InputArgument.op_Implicit(10)
                });
              }
            }
            this.CurrentPlayer.get_Character().set_MaxDrivingSpeed(this.visualSpeed);
            ((Entity) this.CurrentVehicle).set_Speed(this.visualSpeed);
            this.CurrentPlayer.get_Character().set_DrivingSpeed(this.visualSpeed);
            if (this.currentCourse != null)
              this.debugGenInfo.DebugString += string.Format("Distance to WPT#{0} = {1} current WPT  {2}\n", (object) this.currentCourse.currentWaypointIndex, (object) World.GetDistance(((Entity) this.CurrentVehicle).get_Position(), this.currentCourse.GetCurrentWaypoint()), (object) this.currentCourse.GetCurrentWaypoint());
            this.debugGenInfo.DebugString += string.Format("Autodriving {0} visualSpeed={1} veh speed={2}\n", (object) this.autoPilot, (object) this.visualSpeed, (object) ((Entity) this.CurrentVehicle).get_Speed());
            this.debugGenInfo.DebugString += string.Format("Position ({0}) Heading:{1}\n", (object) ((Entity) this.CurrentVehicle).get_Position(), (object) ((Entity) this.CurrentVehicle).get_Heading());
            Model model;
            if (num1 > 0)
            {
              model = ((Entity) this.CurrentVehicle).get_Model();
              if (((Model) ref model).get_IsBicycle())
              {
                if ((double) this.visualSpeed < 11.0)
                  Game.SetControlValueNormalized((Control) 136, 1f);
                else
                  Game.SetControlValueNormalized((Control) 137, 1f);
              }
            }
            int genericCommand = DataTransceiver.GenericCommand;
            if (genericCommand != (int) byte.MaxValue)
            {
              this.debugGenInfo.DebugString += string.Format("remote Control={0}", (object) genericCommand);
              if (genericCommand == 0)
                Game.SetControlValueNormalized((Control) 59, 1f);
              else if (genericCommand == 1)
                Game.SetControlValueNormalized((Control) 59, -1f);
              else if (genericCommand == 2)
                Game.SetControlValueNormalized((Control) 86, 1f);
            }
            this.debugGenInfo.DebugString += string.Format("\nSteer={0} - moveLR={1}\n", (object) Game.GetControlValue((Control) 59), (object) this.moveLeftRight);
            if ((double) this.moveLeftRight != 0.0)
              Game.SetControlValueNormalized((Control) 59, this.moveLeftRight);
            Vector3 position = ((Entity) this.CurrentVehicle).get_Position();
            Vector3 vector3_1 = position;
            ref __Null local = ref vector3_1.Z;
            // ISSUE: cast to a reference type
            // ISSUE: explicit reference operation
            // ISSUE: cast to a reference type
            // ISSUE: explicit reference operation
            ^(float&) ref local = ^(float&) ref local - 20f;
            RaycastResult raycastResult1 = World.Raycast(position, vector3_1, (IntersectFlags) 1, (Entity) this.CurrentVehicle);
            if (((RaycastResult) ref raycastResult1).get_DidHit())
              DataTransceiver.TerrainFriction = Material.getMaterialFriction(((RaycastResult) ref raycastResult1).get_MaterialHash());
            DataTransceiver.GroundElevation = World.GetGroundHeight(Vector3.op_Implicit(position)) * this.slopeScale;
            this.smoothedSlope = (float) ((double) this.smoothedSlope * 0.990000009536743 + (double) ((float) (((double) World.GetGroundHeight(Vector3.op_Addition(((Entity) this.CurrentVehicle).get_Position(), Vector3.op_Multiply(((Entity) this.CurrentVehicle).get_ForwardVector(), 1f))) - (double) World.GetGroundHeight(Vector3.op_Addition(((Entity) this.CurrentVehicle).get_Position(), Vector3.op_Multiply(((Entity) this.CurrentVehicle).get_ForwardVector(), -1f)))) / 2.0) * this.slopeScale) * 0.00999999977648258);
            DataTransceiver.Slope = this.smoothedSlope;
            DataTransceiver.DraftingCoeff = 1f;
            Vector3 forwardVector = ((Entity) this.CurrentVehicle).get_ForwardVector();
            RaycastResult raycastResult2 = World.Raycast(position, Vector3.op_Addition(position, Vector3.op_Multiply(forwardVector, 10f)), (IntersectFlags) -1, (Entity) this.CurrentVehicle);
            if (((RaycastResult) ref raycastResult2).get_DidHit())
            {
              float num2 = World.GetDistance(((RaycastResult) ref raycastResult2).get_HitPosition(), position) - 1f;
              if (Entity.op_Inequality(((RaycastResult) ref raycastResult2).get_HitEntity(), (Entity) null))
                this.debugGenInfo.DebugString += string.Format("Collided entity heading {0}\n", (object) ((RaycastResult) ref raycastResult2).get_HitEntity().get_Heading());
              if (Entity.op_Inequality(((RaycastResult) ref raycastResult2).get_HitEntity(), (Entity) null) && ((RaycastResult) ref raycastResult2).get_HitEntity().get_EntityType() == 2 && ((double) ((RaycastResult) ref raycastResult2).get_HitEntity().get_Heading() < (double) ((Entity) this.CurrentVehicle).get_Heading() + 45.0 && (double) ((RaycastResult) ref raycastResult2).get_HitEntity().get_Heading() > (double) ((Entity) this.CurrentVehicle).get_Heading() - 45.0))
              {
                model = ((RaycastResult) ref raycastResult2).get_HitEntity().get_Model();
                (Vector3, Vector3) dimensions = ((Model) ref model).get_Dimensions();
                float num3 = (float) ((dimensions.Item2.X - dimensions.Item1.X) * (dimensions.Item2.Z - dimensions.Item1.Z));
                DataTransceiver.DraftingCoeff = System.Math.Min((float) (1.24000000953674 / (double) num3 - 0.0104000000283122 * (double) num2 + 0.0452000014483929 * (double) num2 * (double) num2), 1f);
                this.debugGenInfo.DebugString += string.Format("\nveh={0}\ndist={1:F1} area={2:F2}\ndraft={3}", (object) Game.GetLocalizedString(((Vehicle) ((RaycastResult) ref raycastResult2).get_HitEntity()).get_DisplayName()), (object) num2, (object) num3, (object) DataTransceiver.DraftingCoeff);
              }
              else if ((double) num2 < (double) ((Entity) this.CurrentVehicle).get_Speed())
              {
                Vector3 vector3_2 = Vector3.Reflect(((Entity) this.CurrentVehicle).get_ForwardVector(), ((RaycastResult) ref raycastResult2).get_SurfaceNormal());
                if ((double) ((Entity) this.CurrentVehicle).get_Heading() - (double) ((Vector3) ref vector3_2).ToHeading() > 0.0)
                {
                  this.debugGenInfo.DebugString += "TURNING RIGTH to avoid collision\n";
                  Game.SetControlValueNormalized((Control) 59, 1f);
                }
                else
                {
                  this.debugGenInfo.DebugString += "TURNING LEFT to avoid collision\n";
                  Game.SetControlValueNormalized((Control) 59, -1f);
                }
              }
            }
            DataTransceiver.PlayerWeight = this.userWeight;
            DataTransceiver.VehicleWeight = HandlingData.GetByVehicleModel(((Entity) this.CurrentVehicle).get_Model()).get_Mass() / 10f;
            DataTransceiver.VehicleFrontArea = !this.CurrentPlayer.get_Character().get_IsOnBike() ? 0.2f : this.selectedBike.FrontalArea;
            this.currentActivity?.Update(((Entity) this.CurrentVehicle).get_Position(), DataTransceiver.TotalDistance_m, (double) this.visualSpeed > 0.0 ? this.visualSpeed : 0.0f, DataTransceiver.Cadence_rpm, DataTransceiver.Power_w, DataTransceiver.GroundElevation, DataTransceiver.Slope);
            if (this.nextCameraChangeTicks != 0 && this.cameraHeading != this.finalCameraHeading)
            {
              if (this.finalCameraHeading > this.cameraHeading)
                this.cameraHeading += 3;
              else
                this.cameraHeading -= 3;
              GameplayCamera.set_RelativeHeading((float) this.cameraHeading);
            }
            this.debugGenInfo.DebugString += string.Format("Next camera change in {0} final camera heading {1}={2}?\n", (object) this.nextCameraChangeTicks, (object) this.finalCameraHeading, (object) this.cameraHeading);
            HUD.Show();
            goto label_53;
          }
        }
        this.bikeSpawn = false;
        return;
      }
label_53:
      if (!this.CurrentPlayer.get_Character().get_IsOnFoot() && !Hud.IsComponentActive((HudComponent) 6))
      {
        if (Function.Call<bool>((Hash) 3098004816684799861L, new InputArgument[1]
        {
          Entity.op_Implicit((Entity) this.CurrentPlayer.get_Character())
        }) == null)
          goto label_56;
      }
      HUD.Hide();
label_56:
      if (this.CurrentPlayer.get_Character().get_IsOnFoot() && DataTransceiver.IsOK && Entity.op_Inequality((Entity) this.LastVehicle, (Entity) null))
      {
        if ((double) DataTransceiver.Speed_ms == 0.0 && (DataTransceiver.Cadence_rpm == 0 || DataTransceiver.Cadence_rpm == (int) byte.MaxValue))
        {
          Notification.Hide(this.speedToZeroWarningHandle);
          this.speedToZeroWarningHandle = 0;
          this.multipurposeValue = (object) (SubTaskUtils.GetActiveTasks(this.CurrentPlayer.get_Character()) + "  dist=" + (object) World.GetDistance(((Entity) this.CurrentPlayer.get_Character()).get_Position(), ((Entity) this.LastVehicle).get_Position()));
          if ((double) World.GetDistance(((Entity) this.CurrentPlayer.get_Character()).get_Position(), ((Entity) this.LastVehicle).get_Position()) < 2.0 && !SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskEnterVehicle))
            this.CurrentPlayer.get_Character().get_Task().EnterVehicle(this.LastVehicle, (VehicleSeat) -1, -1, 1f, (EnterVehicleFlags) 0);
          else if (!SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.get_Character(), SubTaskUtils.SubTask.CTaskDoNothing))
          {
            this.CurrentPlayer.get_Character().get_Task().GoTo(((Entity) this.LastVehicle).get_Position(), -1);
          }
          else
          {
            // ISSUE: reference to a compiler-generated field
            if (Cyclist.\u003C\u003Eo__54.\u003C\u003Ep__0 == null)
            {
              // ISSUE: reference to a compiler-generated field
              Cyclist.\u003C\u003Eo__54.\u003C\u003Ep__0 = CallSite<Func<CallSite, object, string, object>>.Create(Binder.BinaryOperation(CSharpBinderFlags.None, ExpressionType.AddAssign, typeof (Cyclist), (IEnumerable<CSharpArgumentInfo>) new CSharpArgumentInfo[2]
              {
                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, (string) null),
                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, (string) null)
              }));
            }
            // ISSUE: reference to a compiler-generated field
            // ISSUE: reference to a compiler-generated field
            this.multipurposeValue = Cyclist.\u003C\u003Eo__54.\u003C\u003Ep__0.Target((CallSite) Cyclist.\u003C\u003Eo__54.\u003C\u003Ep__0, this.multipurposeValue, " trapped subtask= " + SubTaskUtils.GetActiveTasks(this.CurrentPlayer.get_Character()));
          }
        }
        else if (this.speedToZeroWarningHandle == 0)
          this.speedToZeroWarningHandle = Notification.Show("Stop pedaling to return to vehicle", false);
      }
      if (!this.CurrentPlayer.get_Character().get_IsOnFoot() && DataTransceiver.IsOK && (double) DataTransceiver.Speed_ms > 0.0)
      {
        this.menuPool.CloseAllMenus();
        if (this.currentActivity == null)
          this.currentActivity = new Activity(this.settings);
      }
      this.debugGenInfo.DebugString += string.Format("Value {0}\n onfoot {1} lastveh={2}\n", this.multipurposeValue, (object) this.CurrentPlayer.get_Character().get_IsOnFoot(), (object) (Entity.op_Inequality((Entity) this.LastVehicle, (Entity) null) ? Game.GetLocalizedString(this.LastVehicle.get_DisplayName()) ?? "" : "None"));
      DataTransceiver.Update();
      this.RefreshHUD();
      this.debugGenInfo.Draw();
      Timer.Update();
    }

    private void onKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.NumPad4)
      {
        this.moveLeftRight = -0.4f;
      }
      else
      {
        if (e.KeyCode != Keys.NumPad6)
          return;
        this.moveLeftRight = 0.4f;
      }
    }

    private void onKeyUp(object sender, KeyEventArgs e)
    {
      if (Debug.Active)
      {
        if (e.KeyCode == Keys.NumPad0)
        {
          DataTransceiver.Start(this.CurrentPlayer, (ushort) this.settings.GetValue<ushort>("main", "FECDeviceId", (M0) 0), (ushort) this.settings.GetValue<ushort>("main", "ControlsDeviceId", (M0) 12345));
          if (!this.bikeSpawn && Entity.op_Equality((Entity) this.CurrentVehicle, (Entity) null))
            this.SpawnBikeAndBiker();
        }
        else if (e.KeyCode == Keys.NumPad2)
        {
          if (this.currentCourse != null && this.CurrentPlayer.get_Character().IsInVehicle(this.CurrentVehicle))
          {
            this.Teleport(this.currentCourse.GoNextWaypoint());
            Vehicle currentVehicle = this.CurrentVehicle;
            Vector2 vector2 = Vector3.op_Implicit(Vector3.op_Subtraction(this.currentCourse.GetNextWaypoint(), ((Entity) this.CurrentVehicle).get_Position()));
            double heading = (double) ((Vector2) ref vector2).ToHeading();
            ((Entity) currentVehicle).set_Heading((float) heading);
          }
        }
        else if (e.KeyCode == Keys.NumPad1)
        {
          this.lastAutoPilotValue = this.autoPilot;
          this.autoPilot = !this.autoPilot;
          this.autodriveMenuItem.Checked = this.autoPilot;
          this.CurrentPlayer.get_Character().get_Task().ClearAll();
        }
        else if (e.KeyCode != Keys.NumPad3 && e.KeyCode != Keys.NumPad5)
        {
          if (e.KeyCode == Keys.NumPad4 || e.KeyCode == Keys.NumPad6)
            this.moveLeftRight = 0.0f;
          else if (e.KeyCode == Keys.NumPad9)
            this.currentCourse?.EditPreviousWayPoint(((Entity) this.CurrentVehicle).get_Position());
          else if (e.KeyCode != Keys.NumPad7 && e.KeyCode == Keys.NumPad8)
            this.currentCourse?.InsertWayPoint(((Entity) this.CurrentVehicle).get_Position());
        }
      }
      if (e.KeyCode != Keys.F11)
        return;
      Debug.Toggle();
    }

    private void onAbort(object sender, EventArgs e)
    {
      this.DeinitializeMod();
    }

    private void DeinitializeMod()
    {
      if (this.CurrentPlayer.get_Character().get_IsOnBike() && Entity.op_Inequality((Entity) this.CurrentVehicle, (Entity) null))
        this.CurrentPlayer.get_Character().get_Task().LeaveVehicle(this.CurrentVehicle, false);
      ((Entity) this.CurrentVehicle)?.MarkAsNoLongerNeeded();
      DataTransceiver.Stop();
      this.currentActivity?.Encode(0.0f);
      this.currentCourse?.Deinitialize();
    }

    private void InitializeCourse(string fileName)
    {
      if (!DataTransceiver.IsOK)
        return;
      this.currentCourse?.Deinitialize();
      this.currentCourse = Course.CreateCourse(fileName);
      if (this.currentCourse == null)
        return;
      Vector3 startPoint = this.currentCourse.GetStartPoint();
      if (Entity.op_Inequality((Entity) this.CurrentVehicle, (Entity) null))
      {
        if (!this.CurrentPlayer.get_Character().IsInVehicle(this.CurrentVehicle))
          this.CurrentPlayer.get_Character().SetIntoVehicle(this.CurrentVehicle, (VehicleSeat) -1);
        this.Teleport(startPoint);
        this.currentCourse.InitializeCourse();
        this.waypointPositionChanged = true;
        Vehicle currentVehicle = this.CurrentVehicle;
        Vector2 vector2 = Vector3.op_Implicit(Vector3.op_Subtraction(this.currentCourse.GetCurrentWaypoint(), ((Entity) this.CurrentVehicle).get_Position()));
        double heading = (double) ((Vector2) ref vector2).ToHeading();
        ((Entity) currentVehicle).set_Heading((float) heading);
      }
      DataTransceiver.Teleport(((Entity) this.CurrentPlayer.get_Character()).get_Position());
      this.currentActivity?.Encode(0.0f);
      this.currentActivity = new Activity(this.settings);
    }

    private void Teleport(Vector3 location)
    {
      ((Entity) this.CurrentVehicle).set_Position(location);
      float groundHeight;
      do
      {
        Script.Wait(50);
        groundHeight = World.GetGroundHeight(Vector3.op_Implicit(location));
      }
      while ((double) groundHeight == 0.0);
      location.Z = (__Null) (double) groundHeight;
      ((Entity) this.CurrentVehicle).set_Position(location);
    }

    private void SpawnBikeAndBiker()
    {
      this.SpawnBeep();
      Ped character = this.CurrentPlayer.get_Character();
      Model model1 = this.selectedBike.Model;
      if (!((Model) ref model1).get_IsInCdImage() || !((Model) ref model1).get_IsValid() || !((Model) ref model1).get_IsBicycle())
        return;
      ((Model) ref model1).Request(250);
      while (!((Model) ref model1).get_IsLoaded())
        Script.Yield();
      Vector3 offsetPosition = ((Entity) character).GetOffsetPosition(new Vector3(0.0f, 5f, 0.0f));
      if (character.get_IsOnFoot())
      {
        if (Entity.op_Inequality((Entity) this.CurrentVehicle, (Entity) null))
          ((Entity) this.CurrentVehicle).MarkAsNoLongerNeeded();
        this.CurrentVehicle = World.CreateVehicle(model1, offsetPosition, 0.0f);
        Random random = new Random();
        this.CurrentVehicle.get_Mods().set_CustomPrimaryColor(Color.FromArgb(random.Next((int) byte.MaxValue), random.Next((int) byte.MaxValue), random.Next((int) byte.MaxValue)));
        ((Entity) this.CurrentVehicle).set_Heading(((Entity) character).get_Heading());
        character.SetIntoVehicle(this.CurrentVehicle, (VehicleSeat) -1);
      }
      PedComponent[] allComponents = character.get_Style().GetAllComponents();
      PedProp[] allProps = character.get_Style().GetAllProps();
      Model model2 = ((Entity) character).get_Model();
      PedHash hash = (PedHash) ((Model) ref model2).get_Hash();
      if (hash != 225514697)
      {
        if (hash != -1692214353)
        {
          if (hash == -1686040670)
          {
            allComponents[3].SetVariation(10, 0);
            allComponents[4].SetVariation(10, 0);
            allComponents[5].SetVariation(5, 0);
            allComponents[6].SetVariation(4, 0);
            allProps[1].SetVariation(12, 0);
            allProps[0].SetVariation(24, 0);
          }
        }
        else
        {
          allComponents[3].SetVariation(5, 0);
          allComponents[4].SetVariation(5, 0);
          allComponents[5].SetVariation(5, 0);
          allComponents[6].SetVariation(3, 0);
          allProps[1].SetVariation(12, 0);
          allProps[0].SetVariation(3, 0);
        }
      }
      else
      {
        allComponents[3].SetVariation(13, 0);
        allComponents[4].SetVariation(12, 0);
        allComponents[5].SetVariation(9, 0);
        allComponents[6].SetVariation(8, 0);
        allProps[1].SetVariation(4, 0);
        allProps[0].SetVariation(8, 0);
      }
      this.bikeSpawn = true;
    }

    private void SpawnBeep()
    {
      Audio.PlaySoundFrontend("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
    }

    private void RefreshHUD()
    {
      if (!DataTransceiver.IsOK)
        return;
      this.hudData.distance = DataTransceiver.TotalDistance_m;
      this.hudData.power = DataTransceiver.Power_w < 0 ? 0 : DataTransceiver.Power_w;
      this.hudData.speed = this.visualSpeed;
      this.hudData.cadence = DataTransceiver.Cadence_rpm < 0 ? 0 : DataTransceiver.Cadence_rpm;
      this.hudData.time = DataTransceiver.RidingTime_ms;
      this.hudData.windSpeed = (double) DataTransceiver.WindSpeed_kmh < 0.0 ? 0.0f : DataTransceiver.WindSpeed_kmh;
      this.hudData.windDir = DataTransceiver.WindDir_rad;
      this.hudData.slope = DataTransceiver.Slope;
      this.hudData.virtualSpeed = DataTransceiver.IsSpeedVirtual;
      HUD.DrawDashboard(this.hudData);
    }

    private void createMenus()
    {
      this.menuPool = new MenuPool();
      UIMenu mainMenu = new UIMenu("GTBike V", "~b~Bicycle Trainer");
      this.menuPool.Add(mainMenu);
      this.activateModMenuItem = new UIMenuItem("Activate mod", "Spawns a bike and change your clothes to set you ready for the ride!");
      this.activateModMenuItem.SetLeftBadge(UIMenuItem.BadgeStyle.Star);
      mainMenu.AddItem(this.activateModMenuItem);
      mainMenu.OnItemSelect += (ItemSelectEvent) ((sender, item, index) =>
      {
        if (item != this.activateModMenuItem)
          return;
        if (!DataTransceiver.IsOK)
        {
          DataTransceiver.Start(this.CurrentPlayer, (ushort) this.settings.GetValue<ushort>("main", "FECDeviceId", (M0) 0), (ushort) this.settings.GetValue<ushort>("main", "ControlsDeviceId", (M0) 12345));
          this.SpawnBikeAndBiker();
          Notification.Show("GTBike V mod activated", false);
          this.activateModMenuItem.SetRightBadge(UIMenuItem.BadgeStyle.Tick);
          this.autodriveMenuItem.Enabled = true;
          this.courseListMenuItem.Enabled = true;
          this.weightMenuItem.Enabled = true;
          this.saveActivityMenuItem.Enabled = true;
        }
        else
        {
          this.DeinitializeMod();
          Notification.Show("GTBike V mod deactivated", false);
          this.activateModMenuItem.SetRightBadge(UIMenuItem.BadgeStyle.None);
          this.autodriveMenuItem.Enabled = false;
          this.courseListMenuItem.Enabled = false;
          this.weightMenuItem.Enabled = false;
          this.saveActivityMenuItem.Enabled = false;
        }
      });
      this.autodriveMenuItem = new UIMenuCheckboxItem("Auto drive", this.autoPilot, "The vehicle steers automatically");
      this.autodriveMenuItem.Enabled = false;
      mainMenu.AddItem((UIMenuItem) this.autodriveMenuItem);
      mainMenu.OnCheckboxChange += (CheckboxChangeEvent) ((sender, item, checked_) =>
      {
        if (item != this.autodriveMenuItem || !this.autodriveMenuItem.Enabled)
          return;
        this.autoPilot = checked_;
        Notification.Show("Autopilot is " + (this.autoPilot ? "ON" : "OFF"), false);
      });
      this.courseListMenuItem = new UIMenuListItem("Courses", Course.CourseList(Cyclist.UserSettingsPath), 0, "Choose a course from the list");
      this.courseListMenuItem.Enabled = false;
      mainMenu.AddItem((UIMenuItem) this.courseListMenuItem);
      mainMenu.OnListChange += (ListChangedEvent) ((sender, item, index) =>
      {
        if (item != this.courseListMenuItem || !this.courseListMenuItem.Enabled || index == 0)
          return;
        this.InitializeCourse(string.Format("{0}{1}{2}", (object) Cyclist.UserSettingsPath, item.Items[index], (object) ".json"));
        this.lastAutoPilotValue = this.autoPilot;
        this.autoPilot = true;
        this.autodriveMenuItem.Checked = this.autoPilot;
        Notification.Show(string.Format("Loaded course {0}", item.Items[index]), false);
      });
      this.saveActivityMenuItem = new UIMenuItem("End and save current activity", "Ends current activity, saves to FIT file and starts a new one.");
      this.saveActivityMenuItem.Enabled = false;
      mainMenu.AddItem(this.saveActivityMenuItem);
      mainMenu.OnItemSelect += (ItemSelectEvent) ((sender, item, index) =>
      {
        if (item != this.saveActivityMenuItem || !this.saveActivityMenuItem.Enabled)
          return;
        this.currentActivity?.Encode(0.0f);
        this.currentActivity = new Activity(this.settings);
      });
      this.weightMenuItem = new UIMenuSliderItem(string.Format("User weight ({0}Kg)", (object) this.userWeight), "Sets the weight of the rider");
      this.weightMenuItem.Maximum = 1500;
      this.weightMenuItem.Multiplier = 5;
      this.weightMenuItem.Enabled = false;
      this.weightMenuItem.Value = (int) ((double) this.userWeight * 10.0);
      mainMenu.AddItem((UIMenuItem) this.weightMenuItem);
      mainMenu.OnSliderChange += (SliderChangedEvent) ((sender, item, index) =>
      {
        if (item != this.weightMenuItem || !this.weightMenuItem.Enabled)
          return;
        if ((double) item.Value < 400.0)
          item.Value = 400;
        else if ((double) item.Value > 1500.0)
          item.Value = 1500;
        this.userWeight = (float) item.Value / 10f;
        this.weightMenuItem.Text = string.Format("User weight ({0}Kg)", (object) this.userWeight);
        this.userWeightChanged = true;
      });
      mainMenu.OnIndexChange += (IndexChangedEvent) ((sender, index) =>
      {
        if (!this.userWeightChanged)
          return;
        this.settings.SetValue<float>("main", "UserWeightKg", (M0) (double) this.userWeight);
        this.settings.Save();
        this.userWeightChanged = false;
      });
      this.menuPool.RefreshIndex();
      this.add_Tick((EventHandler) ((o, e) => this.menuPool.ProcessMenus()));
      this.add_KeyDown((KeyEventHandler) ((o, e) =>
      {
        if (e.KeyCode != Keys.F5)
          return;
        mainMenu.Visible = !mainMenu.Visible;
      }));
    }

    private class Bicycle
    {
      public Model Model;
      public float Mass;
      public float FrontalArea;

      public Bicycle(Model m, float s, float f)
      {
        this.Model = m;
        this.Mass = s;
        this.FrontalArea = f;
      }
    }
  }
}
