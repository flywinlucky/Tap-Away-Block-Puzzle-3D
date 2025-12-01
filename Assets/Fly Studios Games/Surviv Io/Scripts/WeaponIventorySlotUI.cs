using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponIventorySlotUI : MonoBehaviour
{
    public Text weapon_Slot_Index;
    public Image weapon_Slot_Icon;
    public Text weapon_Slot_AllAmoCount;
    public Text weapon_Slot_Name;
    [Space]
    public Color default_Card_Color;
    public Color selected_Card_Color;
    private Image cardSlotImage;

    public void Start()
    {
        cardSlotImage = GetComponent<Image>();
    }

    // Refresh contents; selected can be used to add a small visual cue (optional)
    public void RefreshSlot(Sprite icon, int indexOneBased, int totalAmmo, string weaponName, bool selected)
    {
        bool hasWeapon = icon != null && !string.IsNullOrEmpty(weaponName);

        // Index always visible & numbered
        if (weapon_Slot_Index != null)
        {
            weapon_Slot_Index.gameObject.SetActive(true);
            weapon_Slot_Index.text = indexOneBased.ToString();
        }

        // Toggle icon based on weapon presence
        if (weapon_Slot_Icon != null)
        {
            weapon_Slot_Icon.gameObject.SetActive(hasWeapon);
            weapon_Slot_Icon.sprite = icon;

            // icon alpha only (keep icon colors neutral so sprite shows correctly)
            float alpha = (selected && hasWeapon) ? 1f : 0.5f;
            weapon_Slot_Icon.color = new Color(1f, 1f, 1f, alpha);
        }

        // ALWAYS apply background (card) color depending on selection, even for empty slots
        if (cardSlotImage != null)
        {
            Color bg = (selected) ? selected_Card_Color : default_Card_Color;
            // keep background alpha as desired (0.5 used previously); adjust if needed
            bg.a = 0.5f;
            cardSlotImage.color = bg;
        }

        if (weapon_Slot_AllAmoCount != null)
        {
            weapon_Slot_AllAmoCount.gameObject.SetActive(hasWeapon);
            weapon_Slot_AllAmoCount.text = hasWeapon ? totalAmmo.ToString() : "";
        }

        if (weapon_Slot_Name != null)
        {
            weapon_Slot_Name.gameObject.SetActive(hasWeapon);
            weapon_Slot_Name.text = hasWeapon ? weaponName : "";
        }
    }
}
