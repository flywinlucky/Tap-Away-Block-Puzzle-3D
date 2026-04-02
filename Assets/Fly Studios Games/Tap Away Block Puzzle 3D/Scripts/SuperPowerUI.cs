using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SuperPowerUI : MonoBehaviour
{
    public Button openBuyPowerPanel_button;
    public Image openBuyPowerPanel_button_image;
    public Sprite close_icon;
    public Sprite plus_icon;
    public Color open_color;
    public Color close_color;
    public GameObject BuyPowerPanel;

    private bool isPanelOpen = false;

    // Start is called before the first frame update
    void Start()
    {
        // Legăm butonul la funcția de toggle
        openBuyPowerPanel_button.onClick.AddListener(ToggleBuyPowerPanel);
        BuyPowerPanel.SetActive(isPanelOpen);
    }

    public void ToggleBuyPowerPanel()
    {
        isPanelOpen = !isPanelOpen; // Inversăm starea panoului
        BuyPowerPanel.SetActive(isPanelOpen); // Activăm sau dezactivăm panoul

        // Actualizăm iconița și culoarea butonului
        openBuyPowerPanel_button_image.sprite = isPanelOpen ? plus_icon : close_icon;
        openBuyPowerPanel_button.image.color = isPanelOpen ? close_color : open_color; // Aplicăm culoarea pe buton
    }
}