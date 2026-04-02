using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopSkinElement : MonoBehaviour
{
    public Text skinName;
    public Text skinPrice;
    public Image skinSprite;
    public Button actionButton; // butonul principal (buy/select)

    // NOU: panel care afișează starea "locked" (lacăt)
    public GameObject lockedPanel;
    // NOU: iconă / obiect UI pentru afişarea costului (de ex. imagine monedă) — se ascunde când e cumpărat
    public GameObject coinIconImage;

    // NOU: sprites pentru buton în funcție de stare
    public Sprite selectedSkinButtonSprite;
    public Sprite deselectedSkinButtonSprite;

    // starea curentă
    private string _skinId;
    private int _price;
    private bool _owned;
    private bool _selected;
    private ShopManager _manager;

    public void Initialize(string id, string name, int price, Sprite icon, bool owned, bool selected, ShopManager manager)
    {
        _skinId = id;
        _manager = manager;
        _price = price;
        _owned = owned;
        _selected = selected;

        if (skinName != null) skinName.text = name;
        if (skinSprite != null) skinSprite.sprite = icon;

        UpdateUI();

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnClicked);
        }

        // Asigurăm starea locked imediat la inițializare
        UpdateLocked();
    }

    private void UpdateUI()
    {
        // folosim skinPrice pentru a afișa fie prețul, fie statusul "Owned"/"Selected"
        if (skinPrice != null)
        {
            if (_owned)
                skinPrice.text = _selected ? "Selected" : "Owned";
            else
                skinPrice.text = _price.ToString();
        }

        // opțional: dezactivăm butonul dacă este "Selected"
        if (actionButton != null)
        {
            actionButton.interactable = !_selected;

            // setăm sprite-ul butonului în funcție de stare (dacă s-au atribuit sprite-urile)
            Image btnImg = actionButton.image;
            if (btnImg != null)
            {
                if (_selected && selectedSkinButtonSprite != null)
                    btnImg.sprite = selectedSkinButtonSprite;
                else if (deselectedSkinButtonSprite != null)
                    btnImg.sprite = deselectedSkinButtonSprite;
                // dacă nu sunt setate sprite-urile, păstrăm sprite-ul existent
            }
        }

        // actualizăm locked panel și iconița de coin în fiecare actualizare UI
        UpdateLocked();
    }

    // NOU: actualizează activarea panelului locked și iconița de coin
    private void UpdateLocked()
    {
        if (lockedPanel != null)
        {
            lockedPanel.SetActive(!_owned);
        }
        if (coinIconImage != null)
        {
            coinIconImage.SetActive(!_owned);
        }
    }

    private void OnClicked()
    {
        if (_manager != null)
        {
            _manager.OnElementClicked(_skinId, _price, _owned);
        }
    }
}
