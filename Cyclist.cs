using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using NativeUI;

namespace GTBikeV
{
	// Token: 0x0200012F RID: 303
	public class Cyclist : Script
	{

		public Vehicle CurrentVehicle { get; private set; }
		private Vehicle LastVehicle { get; set; }
		public Player CurrentPlayer { get; private set; }

		public Cyclist()
		{
			this.modVersion = Assembly.GetExecutingAssembly().GetName().Version;
			this.currentActivity = null;
			this.currentCourse = null;
			this.hudData = new HUD.HUDData();
			this.availableBicycles = new Dictionary<string, Cyclist.Bicycle>
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
			base.Tick += this.onTick;
			base.KeyUp += this.onKeyUp;
			base.KeyDown += this.onKeyDown;
			base.Aborted += this.onAbort;
			base.Interval = 10;
		}

		// Token: 0x06000D82 RID: 3458 RVA: 0x000348D4 File Offset: 0x00032AD4
		private void readConfig()
		{
			CultureInfo.CurrentCulture = new CultureInfo("", false);
			Cyclist.UserDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Rockstar Games\\GTA V\\";
			Cyclist.UserSettingsPath = string.Format("{0}ModSettings\\", Cyclist.UserDataPath);
			if (!Directory.Exists(Cyclist.UserSettingsPath))
			{
				Directory.CreateDirectory(Cyclist.UserSettingsPath);
			}
			string text = string.Format("{0}GTBikeVConfig.ini", Cyclist.UserSettingsPath);
			this.settings = ScriptSettings.Load(text);
			if (!File.Exists(text))
			{
				this.settings.SetValue<string>("main", "SelectedBike", "TRIBIKE");
				this.settings.SetValue<float>("main", "SlopeScale", 0.5f);
				this.settings.SetValue<bool>("main", "DebugWindow", false);
				this.settings.SetValue<float>("main", "InitialGPSPointLat", Activity.DEFAULT_INITIAL_GPS_POINT.X);
				this.settings.SetValue<float>("main", "InitialGPSPointLong", Activity.DEFAULT_INITIAL_GPS_POINT.Y);
				this.settings.SetValue<ushort>("main", "FECDeviceId", 0);
				this.settings.SetValue<ushort>("main", "PWRDeviceId", 0);
				this.settings.SetValue<ushort>("main", "HRDeviceId", 0);
				this.settings.SetValue<ushort>("main", "CADDeviceId", 0);
				this.settings.SetValue<ushort>("main", "FPODDeviceId", 0);
				this.settings.SetValue<ushort>("main", "ControlsDeviceId", 12345);
				this.settings.SetValue<bool>("main", "Imperial", false);
				this.settings.SetValue<float>("main", "UserWeightKg", 75f);
				this.settings.SetValue<bool>("main", "RoadFeel", true);
				this.settings.SetValue<string>("main", "WayPointDefaultColor", "FFADFF2F");
				this.settings.SetValue<float>("main", "WayPointDefaultRadius", 20f);
				this.settings.SetValue<bool>("main", "AlwaysUseVirtualSpeed", false);
				this.settings.SetValue<float>("main", "FineSteeringValue", 0.4f);
				this.settings.SetValue<int>("main", "ScreenshotIntervalKm", 5);
				this.settings.SetValue<int>("main", "ScreenshotXOffset", 0);
				this.settings.Save();
			}
			this.userWeight = this.settings.GetValue<float>("main", "UserWeightKg", 75f);
			if (this.userWeight < 40f || this.userWeight > 150f)
			{
				this.userWeight = 75f;
			}
			this.selectedBike = this.availableBicycles[this.settings.GetValue<string>("main", "SelectedBike", "TRIBIKE")];
			this.slopeScale = this.settings.GetValue<float>("main", "SlopeScale", 0.5f);
			if (this.slopeScale > 2f)
			{
				this.slopeScale = 2f;
			}
			this.activateRoadFeel = this.settings.GetValue<bool>("main", "RoadFeel", true);
			this.debugGenInfo = new Debug(new PointF(0.2f, 0.3f), new SizeF(0.6f, 0.4f), 0.5f, new SizeF(0.01f, 0.01f));
			if (this.settings.GetValue<bool>("main", "DebugWindow", false))
			{
				Debug.Show();
			}
			else
			{
				Debug.Hide();
			}
			this.fineSteeringValue = this.settings.GetValue<float>("main", "FineSteeringValue", 0.4f);
			HUD.SetImperialSystem(this.settings.GetValue<bool>("main", "Imperial", false));
			DataTransceiver.AlwaysUseVirtualSpeed = this.settings.GetValue<bool>("main", "AlwaysUseVirtualSpeed", false);
		}

		// Token: 0x06000D83 RID: 3459 RVA: 0x00034CC8 File Offset: 0x00032EC8
		private void onTick(object sender, EventArgs e)
		{
			this.CurrentPlayer = Game.Player;
			this.CurrentVehicle = this.CurrentPlayer.Character.CurrentVehicle;
			this.debugGenInfo.DebugString = string.Format("Version {0}\n", this.modVersion.ToString());
			if (this.CurrentPlayer.IsPlaying && this.CurrentPlayer.CanControlCharacter && DataTransceiver.IsOK)
			{
				this.CurrentPlayer.IsInvincible = true;
				this.CurrentPlayer.WantedLevel = 0;
				if (CurrentPlayer.IsDead || Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, new InputArgument[]
				{
					this.CurrentPlayer.Handle,
					true
				}))
				{
					return;
				}
				this.visualSpeed = ((DataTransceiver.Speed_ms < 0f) ? 0f : DataTransceiver.Speed_ms);
			//	visualSpeed = visualSpeed * 1.25f;
				int cadence_rpm = (int)DataTransceiver.Cadence_rpm;
				if (this.CurrentVehicle != null && DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_CYCLING)
				{
					this.LastVehicle = this.CurrentVehicle;
					if (this.CurrentVehicle.IsInAir)
					{
						Function.Call(Hash.APPLY_FORCE_TO_ENTITY_CENTER_OF_MASS, new InputArgument[]
						{
							this.CurrentVehicle,
							1,
							0,
							0,
							-10f,
							false,
							false,
							true,
							false
						});
					}
					if ((this.visualSpeed <= 0f || !this.autoPilot) && (SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskControlVehicle) || SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskCarDriveWander)))
					{
						this.CurrentPlayer.Character.Task.ClearAll();
					}
					if (this.visualSpeed > 0f && Timer.timedOut("DRIVE_UPDATE"))
					{
						if (this.currentCourse != null || World.WaypointPosition != Vector3.Zero)
						{
							if (this.currentCourse != null && this.currentCourse.IsCloseToCurrentWaypoint(this.CurrentVehicle.Position))
							{
								this.currentCourse.GoNextWaypoint();
								this.waypointPositionChanged = true;
							}
							if (this.autoPilot && (!SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskControlVehicle) || this.waypointPositionChanged || this.lastAutoPilotValue != this.autoPilot))
							{
								TaskInvoker task = this.CurrentPlayer.Character.Task;
								Vehicle currentVehicle = this.CurrentVehicle;
								Vector3 waypointPosition = World.WaypointPosition;
								Course course = this.currentCourse;
								task.DriveTo(currentVehicle, waypointPosition, (course != null) ? (course.WaypointRadius / (float)2) : 10f, this.visualSpeed, (DrivingStyle)drivingStyle);
								this.lastAutoPilotValue = this.autoPilot;
								this.waypointPositionChanged = false;
							}
						}
						else if (this.autoPilot && !SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskCarDriveWander))
						{
							CurrentPlayer.Character.Task.CruiseWithVehicle(CurrentVehicle, visualSpeed, (DrivingStyle)drivingStyle);
						}
						if (autoPilot)
						{
							Function.Call(Hash.SET_DRIVER_ABILITY, new InputArgument[]
							{
							10
							});
							Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, new InputArgument[]
							{
							10
							});
						}
					}
					this.CurrentVehicle.Speed = this.visualSpeed;
					if (this.autoPilot)
					{
						this.CurrentPlayer.Character.MaxDrivingSpeed = this.visualSpeed;
						this.CurrentPlayer.Character.DrivingSpeed = this.visualSpeed;
					}
					if (this.currentCourse != null)
					{
						Debug debug = this.debugGenInfo;
						debug.DebugString += string.Format("Distance to WPT#{0} = {1} current WPT  {2}\n", this.currentCourse.currentWaypointIndex, World.GetDistance(this.CurrentVehicle.Position, this.currentCourse.GetCurrentWaypoint()), this.currentCourse.GetCurrentWaypoint());
					}
					Debug debug2 = this.debugGenInfo;
					debug2.DebugString += string.Format("Autodriving {0} visualSpeed={1} veh speed={2}\n", this.autoPilot, this.visualSpeed, this.CurrentVehicle.Speed);
					Debug debug3 = this.debugGenInfo;
					debug3.DebugString += string.Format("Position ({0}) Heading:{1}\n", this.CurrentVehicle.Position, this.CurrentVehicle.Heading);
					if (cadence_rpm > 0 && this.visualSpeed > 0f && this.CurrentVehicle.Model.IsBicycle)
					{
						float num = (float)cadence_rpm / 100f;
						if (num < 0.5f)
						{
							num = 0.5f;
						}
						if (this.visualSpeed < 11f)
						{
							Game.SetControlValueNormalized(GTA.Control.VehiclePushbikePedal, num);
						}
						else
						{
							Game.SetControlValueNormalized(GTA.Control.VehiclePushbikeSprint, num);
						}
					}
					int genericCommand = DataTransceiver.GenericCommand;
					if (genericCommand != 255)
					{
						Debug debug4 = this.debugGenInfo;
						debug4.DebugString += string.Format("remote Control={0}", genericCommand);
						if (genericCommand == 0)
						{
							Game.SetControlValueNormalized(GTA.Control.VehicleMoveLeftRight, 1f);
						}
						else if (genericCommand == 1)
						{
							Game.SetControlValueNormalized(GTA.Control.VehicleMoveLeftRight, -1f);
						}
						else if (genericCommand == 2)
						{
							Game.SetControlValueNormalized(GTA.Control.VehicleHorn, 1f);
						}
					}
					int controlValue = Game.GetControlValue(GTA.Control.VehicleMoveLeftRight);
					if (moveLeftRight != 0f)
					{
						Game.SetControlValueNormalized(GTA.Control.VehicleMoveLeftRight, moveLeftRight);
					}
					int controlValue2 = Game.GetControlValue(GTA.Control.VehicleBrake);
					if (controlValue2 == 254)
					{
						DataTransceiver.IncreaseBraking();
					}
					else if (controlValue2 == 127 && DataTransceiver.IsBrakingActive)
					{
						DataTransceiver.SetBraking(0);
					}
					Debug debug5 = this.debugGenInfo;
					debug5.DebugString += string.Format("Steer={0} - moveLR={1} - Brake={2}\n", controlValue, this.moveLeftRight, controlValue2);
					Vector3 position = this.CurrentPlayer.Character.Position;
					Vector3 vector = position;
					vector.Z -= 20f;
					RaycastResult raycastResult = World.Raycast(position, vector, IntersectFlags.Map, CurrentPlayer.Character);
					if (raycastResult.DidHit)
					{
						DataTransceiver.TerrainFriction = Material.getMaterialFriction(raycastResult.MaterialHash);
						if (activateRoadFeel)
						{
							DataTransceiver.TerrainFeel = Material.getMaterialFeelIndex(raycastResult.MaterialHash);
						}
					}
					DataTransceiver.DraftingCoeff = 1f;
					Vector3 forwardVector = CurrentVehicle.ForwardVector;
					RaycastResult raycastResult2 = World.Raycast(position, position + forwardVector * 10f, IntersectFlags.Everything, CurrentVehicle);
					if (raycastResult2.DidHit)
					{
						float num2 = World.GetDistance(raycastResult2.HitPosition, position) - 1f;
						if (raycastResult2.HitEntity != null)
						{
							Debug debug7 = debugGenInfo;
							debug7.DebugString += string.Format("Collided entity heading {0}\n", raycastResult2.HitEntity.Heading);
						}
						//if (raycastResult2.HitEntity != null && raycastResult2.HitEntity.EntityType == 2 && raycastResult2.HitEntity.Heading < CurrentVehicle.Heading + 45f && raycastResult2.HitEntity.Heading > CurrentVehicle.Heading - 45f)
						if (raycastResult2.HitEntity != null && raycastResult2.HitEntity.EntityType == EntityType.Vehicle && raycastResult2.HitEntity.Heading < CurrentVehicle.Heading + 45f && raycastResult2.HitEntity.Heading > CurrentVehicle.Heading - 45f)

						{
							ValueTuple<Vector3, Vector3> dimensions = raycastResult2.HitEntity.Model.Dimensions;
							float num3 = (dimensions.Item2.X - dimensions.Item1.X) * (dimensions.Item2.Z - dimensions.Item1.Z);
							DataTransceiver.DraftingCoeff = Math.Min(1.24f / num3 - 0.0104f * num2 + 0.0452f * num2 * num2, 1f);
							Debug debug7 = this.debugGenInfo;
							debug7.DebugString += string.Format("\nveh={0}\ndist={1:F1} area={2:F2}\ndraft={3}", new object[]
							{
								Game.GetLocalizedString(((Vehicle)raycastResult2.HitEntity).DisplayName),
								num2,
								num3,
								DataTransceiver.DraftingCoeff
							});
						}
						else if (avoidObstacles && num2 < CurrentVehicle.Speed / 2f)
						{
							float num4 = Vector3.Reflect(CurrentVehicle.ForwardVector, raycastResult2.SurfaceNormal).ToHeading();
							if (CurrentVehicle.Heading - num4 > 0f)
							{
								Debug debug8 = debugGenInfo;
								debug8.DebugString += "TURNING RIGHT to avoid collision\n";
								Game.SetControlValueNormalized(GTA.Control.VehicleMoveLeftRight, 1f);
							}
							else
							{
								Debug debug9 = debugGenInfo;
								debug9.DebugString += "TURNING LEFT to avoid collision\n";
								Game.SetControlValueNormalized(GTA.Control.VehicleMoveLeftRight, -1f);
							}
						}
					}
					DataTransceiver.VehicleWeight = HandlingData.GetByVehicleModel(this.CurrentVehicle.Model).Mass / 10f;
					if (this.CurrentPlayer.Character.IsOnBike)
					{
						DataTransceiver.VehicleFrontArea = this.selectedBike.FrontalArea;
					}
					else
					{
						DataTransceiver.VehicleFrontArea = 0.2f;
					}
				}
				else if (this.CurrentVehicle == null && DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_RUNNING)
				{
					if (this.CurrentPlayer.Character.IsInAir)
					{
						Function.Call(Hash.APPLY_FORCE_TO_ENTITY_CENTER_OF_MASS, new InputArgument[]
						{
							this.CurrentPlayer.Character
						});
					}
					if (this.visualSpeed > 0f && Timer.timedOut("DRIVE_UPDATE"))
					{
						if (this.currentCourse != null)
						{
							if (this.currentCourse.IsCloseToCurrentWaypoint(this.CurrentPlayer.Character.Position))
							{
								this.currentCourse.GoNextWaypoint();
								this.waypointPositionChanged = true;
							}
							if (this.autoPilot && (this.waypointPositionChanged || this.lastAutoPilotValue != this.autoPilot || this.CurrentPlayer.Character.Speed < 0.1f))
							{
								this.CurrentPlayer.Character.Task.GoTo(World.WaypointPosition, -1);
								this.lastAutoPilotValue = this.autoPilot;
								this.waypointPositionChanged = false;
							}
						}
					}
					else if (this.visualSpeed == 0f)
					{
						this.CurrentPlayer.Character.Task.ClearAll();
						this.CurrentPlayer.Character.Speed = 0f;
					}
					float controlValueNormalized = Game.GetControlValueNormalized(GTA.Control.MoveLeftRight);
					if (controlValueNormalized != 0f)
					{
						this.CurrentPlayer.Character.Heading -= controlValueNormalized * 2f;
					}
					else if (this.moveLeftRight != 0f)
					{
						this.CurrentPlayer.Character.Heading -= this.moveLeftRight;
					}
					Debug debug10 = this.debugGenInfo;
					debug10.DebugString += string.Format("Steer={0} - moveLR={1}\n", controlValueNormalized, this.moveLeftRight);
					//DataTransceiver.TerrainFriction = Material.getMaterialFriction(282940568); //decimal to hex calcuslation //https://runtime.fivem.net/doc/natives/
					DataTransceiver.DraftingCoeff = 1f;
					if (this.currentCourse != null)
					{
						Debug debug11 = this.debugGenInfo;
						debug11.DebugString += string.Format("Distance to WPT#{0} = {1} current WPT  {2}\n", this.currentCourse.currentWaypointIndex, World.GetDistance(this.CurrentPlayer.Character.Position, this.currentCourse.GetCurrentWaypoint()), this.currentCourse.GetCurrentWaypoint());
					}
					Debug debug12 = this.debugGenInfo;
					debug12.DebugString += string.Format("Autodriving {0} visualSpeed={1} ped speed={2}\n", this.autoPilot, this.visualSpeed, this.CurrentPlayer.Character.Speed);
					Debug debug13 = this.debugGenInfo;
					debug13.DebugString += string.Format("Position ({0}) Heading:{1}\n", this.CurrentPlayer.Character.Position, this.CurrentPlayer.Character.Heading);
				/*	this.changeRunningAnimation(this.visualSpeed);
					Debug debug14 = this.debugGenInfo;
					debug14.DebugString += string.Format("Animation {0}:{1}:{2} Playing anim={3}\n", new object[]
					{
						this.runningAnimation.Dictionary,
						this.runningAnimation.Name,
						this.runningAnimation.Speed,
						Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, new InputArgument[]
						{
							this.CurrentPlayer.Character,
							this.runningAnimation.Dictionary,
							this.runningAnimation.Name,
							3
						})
					});*/
				}
				else if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_UNKNOWN)
				{
					//this.changeRunningAnimation(0f);
				}
				DataTransceiver.PlayerWeight = this.userWeight;
				DataTransceiver.GroundElevation = World.GetGroundHeight(this.CurrentPlayer.Character.Position) * this.slopeScale;
				Vector3 vector2 = this.CurrentPlayer.Character.Position + this.CurrentPlayer.Character.ForwardVector * 1f;
				float num5 = (World.GetGroundHeight(this.CurrentPlayer.Character.Position + this.CurrentPlayer.Character.ForwardVector * 3f) - World.GetGroundHeight(vector2)) / 2f * this.slopeScale;
				if (Math.Abs(num5 - this.smoothedSlope) > 0.15f)
				{
					num5 = this.smoothedSlope;
				}
				this.smoothedSlope = this.smoothedSlope * 0.98f + num5 * 0.02f;
				DataTransceiver.Slope = this.smoothedSlope;
				Activity activity = this.currentActivity;
				if (activity != null)
				{
					activity.Update(this.CurrentPlayer.Character.Position, DataTransceiver.TotalDistance_m, (this.visualSpeed > 0f) ? this.visualSpeed : 0f, (int)DataTransceiver.Cadence_rpm, DataTransceiver.Power_w, DataTransceiver.GroundElevation, DataTransceiver.Slope, DataTransceiver.HeartRate);
				}
			}
			//if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_UNKNOWN || Hud.IsComponentActive(6) || Function.Call<bool>(3098004816684799861L, new InputArgument[]
			if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_UNKNOWN || Hud.IsComponentActive(HudComponent.VehicleName) || Function.Call<bool>(Hash.IS_PED_RUNNING_MOBILE_PHONE_TASK, new InputArgument[]
			{
				this.CurrentPlayer.Character
			}))
			{
				HUD.Hide();
			}
			else
			{
				HUD.Show();
			}
			if (DataTransceiver.Sport != this.lastSportType)
			{
				this.sportTypeChangedCycles++;
				if (this.sportTypeChangedCycles > 3)
				{
					if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_CYCLING)
					{
						this.changeClothingToCyclingKit(this.CurrentPlayer.Character);
						if (this.LastVehicle == null && this.CurrentVehicle == null)
						{
							this.SpawnBike();
						}
					}
					else if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_RUNNING)
					{
						if (this.CurrentVehicle != null && !this.CurrentPlayer.Character.IsOnFoot)
						{
							this.CurrentPlayer.Character.Task.LeaveVehicle(this.CurrentVehicle, false);
						}
						this.changeClothingToRunningKit(this.CurrentPlayer.Character);
					}
					this.lastSportType = DataTransceiver.Sport;
					this.sportTypeChangedCycles = 0;
				}
			}
			if (this.CurrentPlayer.Character.IsOnFoot && DataTransceiver.IsOK && this.LastVehicle != null && DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_CYCLING)
			{
				DataTransceiver.Slope = 0f;
				if (DataTransceiver.Speed_ms == 0f && (DataTransceiver.Cadence_rpm == 0 || DataTransceiver.Cadence_rpm == 255))
				{
					Notification.Hide(this.speedToZeroWarningHandle);
					this.speedToZeroWarningHandle = 0;
					if (World.GetDistance(this.CurrentPlayer.Character.Position, this.LastVehicle.Position) < 2f && !SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskEnterVehicle))
					{
						CurrentPlayer.Character.Task.EnterVehicle(LastVehicle, VehicleSeat.Driver, -1, 1f, 0);
					}
					else if (!SubTaskUtils.IsSubTaskActive(this.CurrentPlayer.Character, SubTaskUtils.SubTask.CTaskDoNothing))
					{
						this.CurrentPlayer.Character.Task.GoTo(this.LastVehicle.Position, -1);
					}
				}
				else if (this.speedToZeroWarningHandle == 0)
				{
					this.speedToZeroWarningHandle = Notification.Show("Stop pedaling to return to vehicle", false);
				}
			}
			//if (DataTransceiver.IsOK && DataTransceiver.Speed_ms > 0.1f)
			    if (DataTransceiver.IsOK && DataTransceiver.Speed_ms > 9f)
				{
				this.menuPool.CloseAllMenus();
				if (this.currentActivity == null)
				{
					this.currentActivity = new Activity(this.settings, DataTransceiver.Sport);
				}
			}
			if (this.LastVehicle != null)
			{
				Game.GetLocalizedString(this.LastVehicle.DisplayName);
			}
			long num6 = (long)Environment.TickCount;
			if (num6 - this.lastPedSpeedTime > 1000L)
			{
				Vector3 vector3 = this.lastPedPosition;
				this.runningSpeed = World.GetDistance(this.lastPedPosition, this.CurrentPlayer.Character.Position) / (float)((num6 - this.lastPedSpeedTime) / 1000L);
				this.lastPedPosition = this.CurrentPlayer.Character.Position;
				this.lastPedSpeedTime = num6;
			}
			else
			{
				Vector3 vector4 = this.lastPedPosition;
			}
			Debug debug15 = this.debugGenInfo;
			//debug15.DebugString += string.Format("\nValue {0}\n", this.multipurposeValue);
			//debug15.DebugString += string.Format("Animation {0}:{1}:{2} Playing anim={3}\n", new object[]
		/*	if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, new InputArgument[]
			{
				this.CurrentPlayer.Character,
				this.runningAnimation.Dictionary,
				this.runningAnimation.Name,
				3
			}))*/
		/*	{
				Debug debug16 = this.debugGenInfo;
				debug16.DebugString += string.Format("\nRunning anim {0}: {1}\n", this.runningAnimation.Dictionary, this.runningAnimation.Name);
			}*/
			DataTransceiver.Update();
			this.RefreshHUD();
			this.debugGenInfo.Draw();
			Timer.Update();
		}

		// Token: 0x06000D84 RID: 3460 RVA: 0x00035F16 File Offset: 0x00034116
		private void onKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.NumPad4)
			{
				this.moveLeftRight = -this.fineSteeringValue;
				return;
			}
			if (e.KeyCode == Keys.NumPad6)
			{
				this.moveLeftRight = this.fineSteeringValue;
			}
		}

		// Token: 0x06000D85 RID: 3461 RVA: 0x00035F48 File Offset: 0x00034148
		private void onKeyUp(object sender, KeyEventArgs e)
		{
			if (Debug.Active && e.KeyCode != Keys.NumPad0)
			{
				if (e.KeyCode == Keys.NumPad2)
				{
					if (this.currentCourse != null && this.CurrentPlayer.Character.IsInVehicle(this.CurrentVehicle))
					{
						this.Teleport(this.currentCourse.GoNextWaypoint());
						this.CurrentVehicle.Heading = (this.currentCourse.GetNextWaypoint() - this.CurrentVehicle.Position).ToHeading();
					}
				}
				else if (e.KeyCode != Keys.NumPad3 && e.KeyCode != Keys.NumPad5)
				{
					if (e.KeyCode == Keys.NumPad9)
					{
						Course course = this.currentCourse;
						if (course != null)
						{
							course.EditPreviousWayPoint(this.CurrentVehicle.Position);
						}
					}
					else if (e.KeyCode == Keys.NumPad8)
					{
						Course course2 = this.currentCourse;
						if (course2 != null)
						{
							course2.InsertWayPoint(this.CurrentVehicle.Position);
						}
					}
				}
			}
			if (e.KeyCode == Keys.F11)
			{
				Debug.Toggle();
				return;
			}
			if (e.KeyCode == Keys.NumPad1)
			{
				this.lastAutoPilotValue = this.autoPilot;
				this.autoPilot = !this.autoPilot;
				this.autodriveMenuItem.Checked = this.autoPilot;
				this.CurrentPlayer.Character.Task.ClearAll();
				/*if (DataTransceiver.Sport == DataTransceiver.SportType.SPORT_TYPE_RUNNING)
				{
					this.lastRunningAnimation.Dictionary = "";
					this.lastRunningAnimation.Name = "";
					this.lastRunningAnimation.Speed = 0f;
					return;
				}*/
			}
			else
			{
				if (e.KeyCode == Keys.NumPad4 || e.KeyCode == Keys.NumPad6)
				{
					this.moveLeftRight = 0f;
					return;
				}
				if (e.KeyCode == Keys.NumPad7)
				{
				Function.Call(Hash.SET_FRONTEND_RADIO_ACTIVE, new InputArgument[]
				{
					true
				});
				Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, new InputArgument[]
				{
					true
				});
				Function.Call(Hash.SET_MOBILE_PHONE_RADIO_STATE, new InputArgument[]
				{
					1
				});
				Function.Call(Hash.SKIP_RADIO_FORWARD, Array.Empty<InputArgument>());
			}
			}
		}

		// Token: 0x06000D86 RID: 3462 RVA: 0x00036170 File Offset: 0x00034370
		private void onAbort(object sender, EventArgs e)
		{
			this.DeinitializeMod();
			this.menuPool = null;
			base.Tick -= this.onTick;
			base.KeyUp -= this.onKeyUp;
			base.KeyDown -= this.onKeyDown;
		}

		// Token: 0x06000D87 RID: 3463 RVA: 0x000361C0 File Offset: 0x000343C0
		private void DeinitializeMod()
		{
			this.CurrentPlayer.IsInvincible = false;
			this.CurrentPlayer.Character.Task.ClearAll();
			if (this.CurrentVehicle != null && this.CurrentPlayer.Character.IsOnBike)
			{
				this.CurrentPlayer.Character.Task.LeaveVehicle(this.CurrentVehicle, false);
			}
			Vehicle currentVehicle = this.CurrentVehicle;
			if (currentVehicle != null)
			{
				currentVehicle.MarkAsNoLongerNeeded();
			}
			this.CurrentVehicle = null;
			this.LastVehicle = null;
			DataTransceiver.Stop();
			Activity activity = this.currentActivity;
			if (activity != null)
			{
				activity.Encode(0f);
			}
			this.currentActivity = null;
			Course course = this.currentCourse;
			if (course != null)
			{
				course.Deinitialize();
			}
			this.currentCourse = null;
			this.lastSportType = DataTransceiver.SportType.SPORT_TYPE_UNKNOWN;
		}

		// Token: 0x06000D88 RID: 3464 RVA: 0x0003628C File Offset: 0x0003448C
		private void InitializeCourse(string fileName)
		{
			if (DataTransceiver.IsOK)
			{
				Course course = this.currentCourse;
				if (course != null)
				{
					course.Deinitialize();
				}
				string value = this.settings.GetValue<string>("main", "WayPointDefaultColor", "FFADFF2F");
				float value2 = this.settings.GetValue<float>("main", "WayPointDefaultRadius", 20f);
				this.currentCourse = Course.CreateCourse(fileName, value, value2);
				if (this.currentCourse != null)
				{
					Vector3 startPoint = this.currentCourse.GetStartPoint();
					if (this.CurrentVehicle != null && !this.CurrentPlayer.Character.IsInVehicle(this.CurrentVehicle))
					{
						CurrentPlayer.Character.SetIntoVehicle(CurrentVehicle, VehicleSeat.Driver);
					}
					else if (this.LastVehicle != null && !this.CurrentPlayer.Character.IsInVehicle(this.LastVehicle))
					{
						this.CurrentVehicle = this.LastVehicle;
						CurrentPlayer.Character.SetIntoVehicle(CurrentVehicle, VehicleSeat.Driver);
					}
					this.Teleport(startPoint);
					this.currentCourse.InitializeCourse();
					if (this.CurrentVehicle != null)
					{
						this.CurrentVehicle.Heading = (this.currentCourse.GetCurrentWaypoint() - this.CurrentVehicle.Position).ToHeading();
					}
					else
					{
						this.CurrentPlayer.Character.Heading = (this.currentCourse.GetCurrentWaypoint() - this.CurrentPlayer.Character.Position).ToHeading();
					}
					this.waypointPositionChanged = true;
					DataTransceiver.Teleport(this.CurrentPlayer.Character.Position);
					Activity activity = this.currentActivity;
					if (activity != null)
					{
						activity.Encode(0f);
					}
					this.currentActivity = new Activity(this.settings, DataTransceiver.Sport);
				}
			}
		}

		// Token: 0x06000D89 RID: 3465 RVA: 0x00036474 File Offset: 0x00034674
		private void Teleport(Vector3 location)
		{
			if (this.CurrentVehicle != null)
			{
				this.CurrentVehicle.Position = location;
			}
			else
			{
				this.CurrentPlayer.Character.Position = location;
			}
			int num = 0;
			float groundHeight;
			do
			{
				Script.Wait(50);
				groundHeight = World.GetGroundHeight(location);
				if (groundHeight == 0f)
				{
					location.Z += 1f;
				}
				num++;
			}
			while (groundHeight == 0f && num < 100);
			if (groundHeight != 0f)
			{
				location.Z = groundHeight;
			}
			if (this.CurrentVehicle != null)
			{
				this.CurrentVehicle.Position = location;
				return;
			}
			this.CurrentPlayer.Character.Position = location;
		}

		// Token: 0x06000D8A RID: 3466 RVA: 0x00036524 File Offset: 0x00034724
		private void SpawnBike()
		{
			this.SpawnBeep();
			Ped character = this.CurrentPlayer.Character;
			Model model = this.selectedBike.Model;
			if (model.IsInCdImage && model.IsValid && model.IsBicycle)
			{
				model.Request(250);
				while (!model.IsLoaded)
				{
					Script.Yield();
				}
				Vector3 offsetPosition = character.GetOffsetPosition(new Vector3(0f, 5f, 0f));
				if (character.IsOnFoot)
				{
					if (this.CurrentVehicle != null)
					{
						this.CurrentVehicle.MarkAsNoLongerNeeded();
					}
					this.CurrentVehicle = World.CreateVehicle(model, offsetPosition, 0f);
					Random random = new Random();
					this.CurrentVehicle.Mods.CustomPrimaryColor = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255));
					this.LastVehicle = this.CurrentVehicle;
					this.CurrentVehicle.Heading = character.Heading;
				}
			}
		}

		// Token: 0x06000D8B RID: 3467 RVA: 0x0003663A File Offset: 0x0003483A
		private void SpawnBeep()
		{
			Audio.PlaySoundFrontend("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
		}

		// Token: 0x06000D8C RID: 3468 RVA: 0x0003664C File Offset: 0x0003484C
		private void RefreshHUD()
		{
			if (DataTransceiver.IsOK)
			{
				this.hudData.distance = DataTransceiver.TotalDistance_m;
				this.hudData.power = ((DataTransceiver.Power_w < 0) ? 0 : DataTransceiver.Power_w);
				this.hudData.speed = this.visualSpeed;
				this.hudData.cadence = (int)DataTransceiver.Cadence_rpm;
				this.hudData.heartRate = (int)DataTransceiver.HeartRate;
				this.hudData.time = DataTransceiver.RidingTime_ms;
				this.hudData.windSpeed = ((DataTransceiver.WindSpeed_kmh < 0f) ? 0f : DataTransceiver.WindSpeed_kmh);
				this.hudData.windDir = DataTransceiver.WindDir_rad;
				this.hudData.slope = DataTransceiver.Slope;
				this.hudData.virtualSpeed = DataTransceiver.IsSpeedVirtual;
				this.hudData.draftingFactor = DataTransceiver.DraftingCoeff;
				this.hudData.sport = DataTransceiver.Sport;
				HUD.DrawDashboard(this.hudData);
			}
		}

		// Token: 0x06000D8D RID: 3469 RVA: 0x00036750 File Offset: 0x00034950
		private void createMenus()
		{
			this.menuPool = new MenuPool();
			UIMenu mainMenu = new UIMenu("GTBike V", "~b~Bicycle Trainer");
			this.menuPool.Add(mainMenu);
			this.activateModMenuItem = new UIMenuItem("Activate mod", "Spawns a bike and change your clothes to set you ready for the ride!");
			this.activateModMenuItem.SetLeftBadge(UIMenuItem.BadgeStyle.Star);
			mainMenu.AddItem(this.activateModMenuItem);
			mainMenu.OnItemSelect += delegate(UIMenu sender, UIMenuItem item, int index)
			{
				if (item == this.activateModMenuItem)
				{
					if (!DataTransceiver.IsOK)
					{
						ushort value = this.settings.GetValue<ushort>("main", "FECDeviceId", 0);
						ushort value2 = this.settings.GetValue<ushort>("main", "PWRDeviceId", 0);
						ushort value3 = this.settings.GetValue<ushort>("main", "HRDeviceId", 0);
						ushort value4 = this.settings.GetValue<ushort>("main", "CADDeviceId", 0);
						ushort value5 = this.settings.GetValue<ushort>("main", "FPODDeviceId", 0);
						ushort value6 = this.settings.GetValue<ushort>("main", "ControlsDeviceId", 12345);
						DataTransceiver.Start(this.CurrentPlayer, value, value2, value3, value4, value5, value6);
						Notification.Show("GTBike V mod activated", false);
						this.activateModMenuItem.SetRightBadge(UIMenuItem.BadgeStyle.Tick);
						this.autodriveMenuItem.Enabled = true;
						this.courseListMenuItem.Enabled = true;
						this.weightMenuItem.Enabled = true;
						this.saveActivityMenuItem.Enabled = true;
						return;
					}
					this.DeinitializeMod();
					Notification.Show("GTBike V mod deactivated", false);
					this.activateModMenuItem.SetRightBadge(UIMenuItem.BadgeStyle.None);
					this.autodriveMenuItem.Enabled = false;
					this.courseListMenuItem.Enabled = false;
					this.weightMenuItem.Enabled = false;
					this.saveActivityMenuItem.Enabled = false;
				}
			};
			this.autodriveMenuItem = new UIMenuCheckboxItem("Auto drive", this.autoPilot, "The vehicle steers automatically");
			this.autodriveMenuItem.Enabled = false;
			mainMenu.AddItem(this.autodriveMenuItem);
			mainMenu.OnCheckboxChange += delegate(UIMenu sender, UIMenuCheckboxItem item, bool checked_)
			{
				if (item == this.autodriveMenuItem && this.autodriveMenuItem.Enabled)
				{
					this.autoPilot = checked_;
					this.avoidObstaclesMenuItem.Enabled = !checked_;
					Notification.Show("Autopilot is " + (this.autoPilot ? "ON" : "OFF"), false);
				}
			};
			this.avoidObstaclesMenuItem = new UIMenuCheckboxItem("Avoid obstacles", this.avoidObstacles, "When in auto drive, the vehicle tries to avoid collisions with static objects, not with other vehicles");
			this.avoidObstaclesMenuItem.Enabled = true;
			mainMenu.AddItem(this.avoidObstaclesMenuItem);
			mainMenu.OnCheckboxChange += delegate(UIMenu sender, UIMenuCheckboxItem item, bool checked_)
			{
				if (item == this.avoidObstaclesMenuItem && this.autodriveMenuItem.Enabled && this.avoidObstaclesMenuItem.Enabled)
				{
					this.avoidObstacles = checked_;
					Notification.Show("Obstacle avoidance is " + (this.avoidObstacles ? "ON" : "OFF"), false);
				}
			};
			this.courseListMenuItem = new UIMenuListItem("Courses", Course.CourseList(Cyclist.UserSettingsPath), 0, "Choose a course from the list");
			this.courseListMenuItem.Enabled = false;
			mainMenu.AddItem(this.courseListMenuItem);
			mainMenu.OnListChange += delegate(UIMenu sender, UIMenuListItem item, int index)
			{
				if (item == this.courseListMenuItem && this.courseListMenuItem.Enabled)
				{
					if (index != 0)
					{
						string fileName = string.Format("{0}{1}{2}", Cyclist.UserSettingsPath, item.Items[index], ".json");
						this.InitializeCourse(fileName);
						this.lastAutoPilotValue = this.autoPilot;
						this.autoPilot = true;
						this.autodriveMenuItem.Checked = this.autoPilot;
						if (this.currentCourse != null)
						{
							Notification.Show(string.Format("Loaded course {0}", item.Items[index]), false);
							return;
						}
						Notification.Show(string.Format("Failed to load course {0}", item.Items[index]), false);
						return;
					}
					else
					{
						Course course = this.currentCourse;
						if (course != null)
						{
							course.Deinitialize();
						}
						this.currentCourse = null;
					}
				}
			};
			this.saveActivityMenuItem = new UIMenuItem("End and save current activity", "Ends current activity, saves to FIT file and starts a new one.");
			this.saveActivityMenuItem.Enabled = false;
			mainMenu.AddItem(this.saveActivityMenuItem);
			mainMenu.OnItemSelect += delegate(UIMenu sender, UIMenuItem item, int index)
			{
				if (item == this.saveActivityMenuItem && this.saveActivityMenuItem.Enabled)
				{
					Activity activity = this.currentActivity;
					if (activity != null)
					{
						activity.Encode(0f);
					}
					this.currentActivity = new Activity(this.settings, DataTransceiver.Sport);
				}
			};
			this.weightMenuItem = new UIMenuSliderItem(string.Format("User weight ({0}Kg)", this.userWeight), "Sets the weight of the rider");
			this.weightMenuItem.Maximum = 1500;
			this.weightMenuItem.Multiplier = 5;
			this.weightMenuItem.Enabled = false;
			this.weightMenuItem.Value = (int)(this.userWeight * 10f);
			mainMenu.AddItem(this.weightMenuItem);
			mainMenu.OnSliderChange += delegate(UIMenu sender, UIMenuSliderItem item, int index)
			{
				if (item == this.weightMenuItem && this.weightMenuItem.Enabled)
				{
					if ((float)item.Value < 400f)
					{
						item.Value = 400;
					}
					else if ((float)item.Value > 1500f)
					{
						item.Value = 1500;
					}
					this.userWeight = (float)item.Value / 10f;
					this.weightMenuItem.Text = string.Format("User weight ({0}Kg)", this.userWeight);
					this.userWeightChanged = true;
				}
			};
			mainMenu.OnIndexChange += delegate(UIMenu sender, int index)
			{
				if (this.userWeightChanged)
				{
					this.settings.SetValue<float>("main", "UserWeightKg", this.userWeight);
					this.settings.Save();
					this.userWeightChanged = false;
				}
			};
			this.menuPool.RefreshIndex();
			base.Tick += delegate(object o, EventArgs e)
			{
				this.menuPool.ProcessMenus();
			};
			base.KeyDown += delegate(object o, KeyEventArgs e)
			{
				if (e.KeyCode == Keys.F5)
				{
					mainMenu.Visible = !mainMenu.Visible;
				}
			};
		}

		// Token: 0x06000D8E RID: 3470 RVA: 0x000369EC File Offset: 0x00034BEC
		/*private void changeRunningAnimation(float runningSpeedTarget)
		{
			if (runningSpeedTarget > 0.1f)
			{
				this.runningAnimation = this.getRunningAnimation(this.CurrentPlayer.Character, runningSpeedTarget);
				if (this.lastRunningAnimation.Dictionary != this.runningAnimation.Dictionary || this.lastRunningAnimation.Name != this.runningAnimation.Name || this.CurrentPlayer.Character.Speed == 0f)
				{
					if (!this.lastRunningAnimation.IsEmpty())
					{
						this.CurrentPlayer.Character.Task.ClearAnimation(this.lastRunningAnimation.Dictionary, this.lastRunningAnimation.Name);
					}
					CurrentPlayer.Character.Task.PlayAnimation(runningAnimation.Dictionary, runningAnimation.Name, 8f, -8f, -1, AnimationFlags.Loop, runningAnimation.Speed);
					this.lastRunningAnimation.Dictionary = this.runningAnimation.Dictionary;
					this.lastRunningAnimation.Name = this.runningAnimation.Name;
					this.lastRunningAnimation.Speed = this.runningAnimation.Speed;
				}
				Function.Call(Hash.SET_ENTITY_ANIM_SPEED, new InputArgument[]
				{
					this.CurrentPlayer.Character,
					this.runningAnimation.Dictionary,
					this.runningAnimation.Name,
					this.runningAnimation.Speed
				});
				return;
			}
			if (this.runningAnimation.Dictionary != "")
			{
				this.CurrentPlayer.Character.Task.ClearAnimation(this.runningAnimation.Dictionary, this.runningAnimation.Name);
				Function.Call(Hash.SET_ANIM_RATE, new InputArgument[]
				{
					this.CurrentPlayer,
					1f,
					0,
					false
				});
				this.runningAnimation.Clear();
				this.lastRunningAnimation.Clear();
			}
		}
		*/
		// Token: 0x06000D8F RID: 3471 RVA: 0x00036C2C File Offset: 0x00034E2C
		/*private Cyclist.MoveAnimation getRunningAnimation(Ped player, float runningSpeedTarget)
		{
			Cyclist.MoveAnimation moveAnimation = new Cyclist.MoveAnimation();
			PedHash hash = (PedHash)player.Model.Hash;

			if (hash != PedHash.Michael)
			{
				if (hash != PedHash.Franklin)
				{
					if (hash != PedHash.Trevor)
					{
						moveAnimation.Dictionary = "move_p_m_two";
					}
				}
				else
				{
					moveAnimation.Dictionary = "move_p_m_one";
				}
			}
			else
			{
				moveAnimation.Dictionary = "move_p_m_zero";
			}
			if (runningSpeedTarget < 2.4f)
			{
				moveAnimation.Name = "walk";
				moveAnimation.Speed = runningSpeedTarget / 1.69f;
			}
			else if (runningSpeedTarget >= 2.4f && runningSpeedTarget < 4.6f)
			{
				moveAnimation.Dictionary = "move_m@jog@";
				moveAnimation.Name = "run";
				moveAnimation.Speed = runningSpeedTarget / 3.13f;
			}
			else if (runningSpeedTarget >= 4.6f)
			{
				moveAnimation.Name = "sprint";
				moveAnimation.Speed = runningSpeedTarget / 6.63f;
			}
			return moveAnimation;
		}
		*/
		// Token: 0x06000D90 RID: 3472 RVA: 0x00036D08 File Offset: 0x00034F08
		private void changeClothingToCyclingKit(Ped player)
		{
			PedComponent[] allComponents = player.Style.GetAllComponents();
			PedProp[] allProps = player.Style.GetAllProps();
			PedHash hash = (PedHash)player.Model.Hash;
			if (hash == PedHash.Michael)
			{
				allComponents[3].SetVariation(13, 0);
				allComponents[4].SetVariation(12, 0);
				allComponents[5].SetVariation(9, 0);
				allComponents[6].SetVariation(8, 0);
				allProps[1].SetVariation(4, 0);
				allProps[0].SetVariation(8, 0);
				return;
			}
			if (hash == PedHash.Franklin)
			{
				allComponents[3].SetVariation(5, 0);
				allComponents[4].SetVariation(5, 0);
				allComponents[5].SetVariation(5, 0);
				allComponents[6].SetVariation(3, 0);
				allProps[1].SetVariation(12, 0);
				allProps[0].SetVariation(3, 0);
				return;
			}
			if (hash == PedHash.Trevor)
			{
				return;
			}
			allComponents[3].SetVariation(10, 0);
			allComponents[4].SetVariation(10, 0);
			allComponents[5].SetVariation(5, 0);
			allComponents[6].SetVariation(4, 0);
			allProps[1].SetVariation(12, 0);
			allProps[0].SetVariation(24, 0);
		}

		// Token: 0x06000D91 RID: 3473 RVA: 0x00036E28 File Offset: 0x00035028
		private void changeClothingToRunningKit(Ped player)
		{
			PedComponent[] allComponents = player.Style.GetAllComponents();
			PedProp[] allProps = player.Style.GetAllProps();
			PedHash hash = (PedHash)player.Model.Hash;
			if (hash == PedHash.Michael)
			{
				allComponents[3].SetVariation(2, 0);
				allComponents[4].SetVariation(23, 0);
				allComponents[5].SetVariation(0, 0);
				allProps[1].SetVariation(7, 0);
				allProps[0].SetVariation(9, 0);
				return;
			}
			if (hash == PedHash.Franklin)
			{
				allComponents[3].SetVariation(31, 0);
				allComponents[4].SetVariation(14, 0);
				allComponents[5].SetVariation(0, 0);
				allComponents[6].SetVariation(0, 0);
				allProps[1].SetVariation(4, 0);
				allProps[0].SetVariation(17, 0);
				return;
			}
			if (hash == PedHash.Trevor)
			{
				return;
			}
			allComponents[3].SetVariation(3, 0);
			allComponents[4].SetVariation(19, 0);
			allComponents[5].SetVariation(0, 0);
			allProps[1].SetVariation(2, 0);
			allProps[0].SetVariation(11, 0);
		}

		// Token: 0x06000D92 RID: 3474 RVA: 0x00036F31 File Offset: 0x00035131
		private void changeClothingToDefault(Ped player)
		{
			player.Style.SetDefaultClothes();
			PedProp[] allProps = player.Style.GetAllProps();
			allProps[1].SetVariation(0, 0);
			allProps[0].SetVariation(0, 0);
		}

		// Token: 0x06000D93 RID: 3475 RVA: 0x00036F60 File Offset: 0x00035160
	/*	private MoveAnimation getCurrentCyclingAnimation(Ped player)
		{
			foreach (MoveAnimation moveAnimation in CyclingAnimations)
			{
				if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, new InputArgument[]
				{
					player,
					moveAnimation.Dictionary,
					moveAnimation.Name,
					3
				}) || Function.Call<bool>(Hash.IS_SCRIPTED_SCENARIO_PED_USING_CONDITIONAL_ANIM, new InputArgument[]
				{
					player,
					moveAnimation.Dictionary,
					moveAnimation.Name
				}))
				{
					return moveAnimation;
				}
			}
			return null;
		}
	*/
		// Token: 0x04000E7A RID: 3706
		private const float SMOOTH_FACTOR = 0.02f;

		// Token: 0x04000E7B RID: 3707
		private const float SLOPE_CALCULATION_DISTANCE = 2f;

		// Token: 0x04000E7C RID: 3708
		private const float SLOPE_ANTICIPATION = 1f;

		// Token: 0x04000E7D RID: 3709
		private const int MAX_DRAFT_DISTANCE = 10;

		// Token: 0x04000E7E RID: 3710
		private const int DRIVE_UPDATE_INTERVAL = 1000;

		// Token: 0x04000E7F RID: 3711
		private const float DEFAULT_SLOPE_SCALE = 0.5f;

		// Token: 0x04000E80 RID: 3712
		private const float DEFAULT_USER_WEIGHT = 75f;

		// Token: 0x04000E81 RID: 3713
		private const float MAX_USER_WEIGHT = 150f;

		// Token: 0x04000E82 RID: 3714
		private const float MIN_USER_WEIGHT = 40f;

		// Token: 0x04000E83 RID: 3715
		private const string DEFAULT_WPT_COLOR = "FFADFF2F";

		// Token: 0x04000E84 RID: 3716
		private const float DEFAULT_WPT_RADIUS = 20f;

		// Token: 0x04000E85 RID: 3717
		private const float MIN_MOVING_SPEED = 0.1f;

		// Token: 0x04000E86 RID: 3718
		private const float DEFAULT_FINE_STEERING = 0.4f;

		// Token: 0x04000E8A RID: 3722
		public static string UserDataPath;

		// Token: 0x04000E8B RID: 3723
		public static string UserSettingsPath;

		// Token: 0x04000E8C RID: 3724
		private Dictionary<string, Cyclist.Bicycle> availableBicycles;

		// Token: 0x04000E8D RID: 3725
		private HUD.HUDData hudData;

		// Token: 0x04000E8E RID: 3726
		private float smoothedSlope;

		// Token: 0x04000E8F RID: 3727
		private bool autoPilot;

		// Token: 0x04000E90 RID: 3728
		private bool avoidObstacles = true;

		// Token: 0x04000E91 RID: 3729
		private float visualSpeed;

		// Token: 0x04000E92 RID: 3730
		private bool lastAutoPilotValue;

		// Token: 0x04000E93 RID: 3731
		private Activity currentActivity;

		// Token: 0x04000E94 RID: 3732
		private Course currentCourse;

		// Token: 0x04000E95 RID: 3733
		private MenuPool menuPool;

		// Token: 0x04000E96 RID: 3734
		private UIMenuItem activateModMenuItem;

		// Token: 0x04000E97 RID: 3735
		private UIMenuCheckboxItem autodriveMenuItem;

		// Token: 0x04000E98 RID: 3736
		private UIMenuCheckboxItem avoidObstaclesMenuItem;

		// Token: 0x04000E99 RID: 3737
		private UIMenuListItem courseListMenuItem;

		// Token: 0x04000E9A RID: 3738
		private UIMenuSliderItem weightMenuItem;

		// Token: 0x04000E9B RID: 3739
		private UIMenuItem saveActivityMenuItem;

		// Token: 0x04000E9C RID: 3740
		private bool userWeightChanged;

		// Token: 0x04000E9D RID: 3741
		private VehicleDrivingFlags drivingStyle = Utils.CustomDrivingStyle;

		// Token: 0x04000E9E RID: 3742
		private float moveLeftRight;

		// Token: 0x04000E9F RID: 3743
		private float fineSteeringValue = 0.4f;

		// Token: 0x04000EA0 RID: 3744
		private int cameraHeading;

		// Token: 0x04000EA1 RID: 3745
		private int finalCameraHeading;

		// Token: 0x04000EA2 RID: 3746
		private int nextCameraChangeTicks;

		// Token: 0x04000EA3 RID: 3747
		private bool waypointPositionChanged;

		// Token: 0x04000EA4 RID: 3748
		private int speedToZeroWarningHandle;

		// Token: 0x04000EA5 RID: 3749
		private float runningSpeed;

		// Token: 0x04000EA6 RID: 3750
		private Vector3 lastPedPosition;

		// Token: 0x04000EA7 RID: 3751
		private long lastPedSpeedTime;

		// Token: 0x04000EA8 RID: 3752
	//	private Cyclist.MoveAnimation runningAnimation = new Cyclist.MoveAnimation();

		// Token: 0x04000EA9 RID: 3753
//		private Cyclist.MoveAnimation lastRunningAnimation = new Cyclist.MoveAnimation();

		// Token: 0x04000EAA RID: 3754
		private DataTransceiver.SportType lastSportType;

		// Token: 0x04000EAB RID: 3755
		private int sportTypeChangedCycles;

		// Token: 0x04000EAC RID: 3756
		private ScriptSettings settings;

		// Token: 0x04000EAD RID: 3757
		private float slopeScale = 0.5f;

		// Token: 0x04000EAE RID: 3758
		private Cyclist.Bicycle selectedBike;

		// Token: 0x04000EAF RID: 3759
		private float userWeight;

		// Token: 0x04000EB0 RID: 3760
		private bool activateRoadFeel;

		// Token: 0x04000EB1 RID: 3761
		private Debug debugGenInfo;

		// Token: 0x04000EB2 RID: 3762
		private Version modVersion;

		// Token: 0x04000EB3 RID: 3763
	//	[Dynamic]
		private dynamic multipurposeValue;

		// Token: 0x04000EB4 RID: 3764
	/*	private List<Cyclist.MoveAnimation> CyclingAnimations = new List<Cyclist.MoveAnimation>
		{
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx@front@base", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@bmx_f@front@base", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiserfront@base", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@cruiser_f@front@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base_fps", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountainfront@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@mountain_f@front@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "tuck_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base_fps", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_left_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_right_downhill_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "wheelie_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "tuck_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@roadfront@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "fast_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "cruise_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "cruise_pedal_right_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "fast_pedal_left_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "cruise_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "fast_pedal_char", 1f),
			new Cyclist.MoveAnimation("veh@bicycle@road_f@front@base", "tuck_pedal_char", 1f)
		};
	*/
		// Token: 0x020001B9 RID: 441
		private class Bicycle
		{
			// Token: 0x06000E47 RID: 3655 RVA: 0x00038547 File Offset: 0x00036747
			public Bicycle(Model m, float s, float f)
			{
				this.Model = m;
				this.Mass = s;
				this.FrontalArea = f;
			}

			// Token: 0x0400169C RID: 5788
			public Model Model;

			// Token: 0x0400169D RID: 5789
			public float Mass;

			// Token: 0x0400169E RID: 5790
			public float FrontalArea;
		}

		
		
		
		/*
		private class MoveAnimation
		{
			
			public MoveAnimation()
			{
				this.Clear();
			}

			
			public MoveAnimation(string d, string n, float s)
			{
				this.Dictionary = d;
				this.Name = n;
				this.Speed = s;
			}

			
			public bool IsEmpty()
			{
				return this.Dictionary == "" && this.Name == "" && this.Speed == 0f;
			}

			
			public void Clear()
			{
				this.Dictionary = "";
				this.Name = "";
				this.Speed = 0f;
			}

			
			public string Dictionary;
			public string Name;
			public float Speed;
		}*/
	}
}
