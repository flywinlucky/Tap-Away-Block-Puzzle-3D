using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponPickUp : MonoBehaviour
{
    public WeaponData weaponData; // ScriptableObject cu datele armei
    private PlayerUI _currentPlayerUI;

    public SpriteRenderer item_color;
    public SpriteRenderer armor_icon;

    // NEW: player currently inside trigger (used for stable input handling)
    private GameObject _playerInRange;

    private void Start()
    {
        // setăm iconița și culoarea din WeaponData
        if (weaponData != null)
        {
            if (armor_icon != null) armor_icon.sprite = weaponData.weaponSpriteIcon;
            if (item_color != null) item_color.color = weaponData.weapon_Color;
        }
    }

    // NEW: centralized input check so pressing F reliably picks up when in trigger
    private void Update()
    {
        if (_playerInRange != null && Input.GetKeyDown(KeyCode.F))
        {
            ApplyPickup(_playerInRange);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ShowPrompt(other.gameObject);
        // mark as in-range only if it looks like a player (has WeaponControler/PlayerUI)
        if (_playerInRange == null && other != null && other.GetComponentInChildren<WeaponControler>() != null)
            _playerInRange = other.gameObject;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // removed direct ApplyPickup call to avoid missed input while moving
        ShowPrompt(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HidePrompt(other.gameObject);
        if (_playerInRange != null && other != null && other.gameObject == _playerInRange)
            _playerInRange = null;
    }

    private void OnDisable()
    {
        // ascundem promptul dacă pickup-ul dispare/este dezactivat
        if (_currentPlayerUI != null)
        {
            _currentPlayerUI.HideFtoSellect();
            _currentPlayerUI = null;
        }
        _playerInRange = null; // ensure cleanup
    }

    private void ShowPrompt(GameObject other)
    {
        if (other == null || weaponData == null) return;

        // arătăm promptul doar pentru player (are WeaponControler)
        var wc = other.GetComponentInChildren<WeaponControler>();
        if (wc == null) return;

        var playerUI = other.GetComponentInChildren<PlayerUI>();
        if (playerUI == null) return;

        playerUI.ShowFtoSellect(weaponData.weaponName);
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
        if (other == null || weaponData == null) return;

        var wc = other.GetComponentInChildren<WeaponControler>();
        var playerUI = other.GetComponentInChildren<PlayerUI>();
        var weaponUI = other.GetComponentInChildren<WeaponUI>();
        if (wc == null) return;

        // dezactivează melee default dacă există
        if (wc.defaultMelee != null)
            wc.defaultMelee.EnableHands(false);

        // echipăm arma din ScriptableObject
        wc.EquipWeapon(weaponData);

        // adaugă în inventory UI și selectează
        if (weaponUI != null) weaponUI.AddWeaponToInventory(weaponData, select: true);

        if (playerUI != null) playerUI.HideFtoSellect();
        Destroy(gameObject);

        // clear tracked player to avoid double pickup
        if (_playerInRange == other) _playerInRange = null;
    }
}
