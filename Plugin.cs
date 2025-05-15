﻿using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using BepInEx.Configuration;

namespace PaCInfo;

[BepInPlugin(GUID: "vespoire.dinkum.controllersprint", Name: "Controller Sprint", Version: "1.0.1")]
public partial class Plugin : BaseUnityPlugin {
  internal static Plugin ActiveInstance;
	private ConfigEntry<float> speed, cost;
	private ConfigEntry<KeyCode> holdKey, toggleKey;
	private bool autoRunToggledOn = false;

  private void Awake() {
    ActiveInstance = this;
    
		Config.Bind("!Developer", "NexusID", -1, "Nexus Mod ID. You can find it on the mod's page on Nexus.");
		speed = Config.Bind("Parameters", "Speed", 0.35f, "Speed bonus while sprinting.");
		cost = Config.Bind("Parameters", "Stamina Cost", 0.2f, "Rate of stamina loss while sprinting.");
		toggleKey = Config.Bind("Controls", "Toggle Key", KeyCode.RightControl, "Press to toggle sprinting on and off.");
		holdKey = Config.Bind("Controls", "Hold Key", KeyCode.LeftControl, "Hold to sprint.");

    Logger.LogInfo("Loaded!");
  }
  

  internal static void Log(string message) {
    ActiveInstance?.Logger.LogInfo(message);
  }

  internal static void LogMany(IEnumerable<string> messages) {
    foreach (var message in messages) Log(message);
  }

  private bool defaultLeftTriggerInteractErased = false;

  private void FixedUpdate() {
    if (CannotSprintRightNow()) return;

    if (! defaultLeftTriggerInteractErased) {
      try {
        var controllerInputs = InputMaster.input.controls;
        var interactInput = new Traverse(controllerInputs).Field("m_Controls_Interact").GetValue<InputAction>();
        interactInput.ChangeBindingWithPath("<Gamepad>/leftTrigger").Erase();

        Log("Erased default left trigger interact.");
        defaultLeftTriggerInteractErased = true;
      } catch {};
    }

    CheckSprintToggle();
    if (StickIsInRunPosition() && IsRunning()) {
      MoveFast();
      SpendStamina();
      PauseStaminaRegen();
    }
  }

  private void CheckSprintToggle() {
    if (Input.GetKeyDown(toggleKey.Value)) autoRunToggledOn = !autoRunToggledOn;
  }

  private void MoveFast() {
    // Use traverse to set private variable
    var playerMovement = NetworkMapSharer.Instance.localChar;
    // playerMovement.CurrentSpeed += speed.Value;
    new Traverse(playerMovement).Property("CurrentSpeed").SetValue(playerMovement.CurrentSpeed + speed.Value);
  }

  private void SpendStamina() {
    var staminaInfo = StatusManager.manage;
    staminaInfo.changeStamina(-cost.Value);
  }

  private static void PauseStaminaRegen() {
    var staminaInfo = StatusManager.manage;
    // Use traverse to read and set private variable
    var currentRegenDelay = new Traverse(staminaInfo).Field("stopStaminaRegenTimer").GetValue<float>();
    var runningDelay = new Traverse(staminaInfo).Field("stopStaminaRegenTimerMax").GetValue<float>();
    // stamina.stopStaminaRegenTimer = Math.Max(stamina.stopStaminaRegenTimer, stamina.stopStaminaRegenTimerMax);
    new Traverse(staminaInfo).Field("stopStaminaRegenTimer").SetValue(Math.Max(currentRegenDelay, runningDelay));
  }

  private bool CannotSprintRightNow() {
    var playerMovement = NetworkMapSharer.Instance?.localChar;
    if (playerMovement is null) return true;

    var staminaInfo = StatusManager.manage;
    if (staminaInfo is null) return true;

    // Use traverse to read private variables
    float staminaAmount = new Traverse(staminaInfo).Field("stamina").GetValue<float>();
    bool staminaPunishmentActive = new Traverse(staminaInfo).Field("staminaPunishment").GetValue<bool>();

    bool driving = playerMovement.driving,    // This is failing to detect driving for SOME purposes but not others. Driving a vehicle in reverse (the sprint controller button) does not consume stamina, but DOES move faster.
      gliding = playerMovement.usingHangGlider,
      tired = staminaInfo.tired;

    return driving || gliding || tired || staminaPunishmentActive || staminaAmount < 2.5 * cost.Value;
  }

  private bool StickIsInRunPosition() {
    return (
      !(InputMaster.input.getLeftStick().x > -0.7f) ||
      !(InputMaster.input.getLeftStick().x < 0.7f) ||
      !(InputMaster.input.getLeftStick().y > -0.7f) ||
      !(InputMaster.input.getLeftStick().y < 0.7f)
    );
  }

  private bool IsRunning() {
    return (
      Input.GetKey(holdKey.Value) ||
      InputMaster.input.VehicleAccelerate() < -0.5f ||    // Detect the controller left trigger.    Bug: vehicles move fast when going backwards.    TODO: Detect when the player is in a vehicle, and suppress sprint behavior.
      autoRunToggledOn
    );
  }
}