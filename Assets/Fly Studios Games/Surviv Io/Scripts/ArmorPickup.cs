using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArmorPickup : MonoBehaviour
{
    public EquipmentData equipmentData; // ScriptableObject containing armor data
    public SpriteRenderer item_color;
    public SpriteRenderer armor_icon;

    // cache UI referință pentru a închide promptul corect
    private PlayerUI _currentPlayerUI;

    private void Start()
    {
        // Set the armor_icon sprite from EquipmentData
        if (armor_icon != null && equipmentData != null)
        {
            armor_icon.sprite = equipmentData.equipmentSpriteIcon;
            item_color.color = equipmentData.equipament_Color;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ShowPrompt(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Input.GetKeyDown(KeyCode.F))
            ApplyPickup(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HidePrompt(other.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        ShowPrompt(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        if (Input.GetKeyDown(KeyCode.F))
            ApplyPickup(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        HidePrompt(other.gameObject);
    }

    private void ShowPrompt(GameObject other)
    {
        if (other == null || equipmentData == null) return;
        var playerUI = other.GetComponentInChildren<PlayerUI>();
        if (playerUI == null) return;

        playerUI.ShowFtoSellect(equipmentData.itemName);
        _currentPlayerUI = playerUI;
    }

    private void HidePrompt(GameObject other)
    {
        var ui = other != null ? other.GetComponentInChildren<PlayerUI>() : _currentPlayerUI;
        if (ui != null) ui.HideFtoSellect();
        if (ui == _currentPlayerUI) _currentPlayerUI = null;
    }

    private void ApplyPickup(GameObject other)
    {
        if (other == null || equipmentData == null) return;

        var playerUI = other.GetComponentInChildren<PlayerUI>();
        var playerHealth = other.GetComponentInChildren<PlayerHealth>();
        if (playerUI == null || playerHealth == null) return;

        // Apply equipment data to the appropriate slot and stochează numele în slot
        if (equipmentData.equipmentType == EquipmentData.EquipmentType.Helmets)
        {
            playerUI.helment_Slot.RefreshEquipamentSlot(equipmentData.equipmentSpriteIcon, equipmentData.equipmentLevel);
        }
        else if (equipmentData.equipmentType == EquipmentData.EquipmentType.Vests)
        {
            playerUI.vest_Slot.RefreshEquipamentSlot(equipmentData.equipmentSpriteIcon, equipmentData.equipmentLevel);
        }

        // Replace the player's current armor with the new maximum armor
        playerHealth.SetArmor(equipmentData.damageReduction);

        PlayPickupSound(); // moved into helper

        Debug.Log($"Armor pickup applied: {equipmentData.equipmentType}, Max Armor: {equipmentData.damageReduction}, Damage Reduction: {equipmentData.damageReduction}%");

        // ascunde promptul și distruge pickup-ul
        if (playerUI != null) playerUI.HideFtoSellect();
        Destroy(gameObject);
    }

    private void PlayPickupSound()
    {
        if (equipmentData != null && equipmentData.equipment_pickup_sound != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play2DSound(equipmentData.equipment_pickup_sound);
            else
                AudioSource.PlayClipAtPoint(equipmentData.equipment_pickup_sound, Vector3.zero);
        }
    }
}