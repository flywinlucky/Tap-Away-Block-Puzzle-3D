using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponPickUp : MonoBehaviour
{
    public WeaponData weaponData; // ScriptableObject cu datele armei
    private PlayerUI _currentPlayerUI;

    public SpriteRenderer item_color;
    public SpriteRenderer armor_icon;

    private void Start()
    {
        // setăm iconița și culoarea din WeaponData
        if (weaponData != null)
        {
            if (armor_icon != null) armor_icon.sprite = weaponData.weaponSpriteIcon;
            if (item_color != null) item_color.color = weaponData.weapon_Color;
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

    private void OnDisable()
    {
        // ascundem promptul dacă pickup-ul dispare/este dezactivat
        if (_currentPlayerUI != null)
        {
            _currentPlayerUI.HideFtoSellect();
            _currentPlayerUI = null;
        }
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
    }
}
