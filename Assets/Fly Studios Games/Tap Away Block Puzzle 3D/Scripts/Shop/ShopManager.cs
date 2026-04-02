using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Setup")]
    public GameObject shopSkinElementPrefab; // Prefab pentru elementele din shop
    public Transform contentRoot; // Unde vom instanția elementele UI
    public List<ShopSkinData> skins = new List<ShopSkinData>(); // Lista de ScriptableObjects

    [Header("References")]
    public LevelManager levelManager; // Referință pentru a aplica skin la blocuri

    private const string SelectedSkinKey = "SelectedSkin";
    private HashSet<string> _owned = new HashSet<string>();
    [HideInInspector] public ShopSkinData selectedSkin;

    private void Start()
    {
        LoadOwned();
        LoadSelected();
        PopulateShop();
    }

    private void LoadOwned()
    {
        _owned.Clear();
        foreach (var skin in skins)
        {
            if (PlayerPrefs.GetInt("SkinOwned_" + skin.id, 0) == 1)
            {
                _owned.Add(skin.id);
            }
        }
    }

    private void LoadSelected()
    {
        string selectedSkinId = PlayerPrefs.GetString(SelectedSkinKey, "");
        selectedSkin = skins.Find(skin => skin.id == selectedSkinId);
    }

    private void PopulateShop()
    {
        if (shopSkinElementPrefab == null || contentRoot == null) return;

        // Curățăm conținutul
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(contentRoot.GetChild(i).gameObject);
        }

        foreach (var skin in skins)
        {
            GameObject go = Instantiate(shopSkinElementPrefab, contentRoot);
            ShopSkinElement el = go.GetComponent<ShopSkinElement>();
            if (el != null)
            {
                bool owned = _owned.Contains(skin.id);
                bool isSelected = selectedSkin == skin;
                el.Initialize(skin.id, skin.displayName, skin.price, skin.icon, owned, isSelected, this);
            }
        }
    }

    // apelat din element UI când jucătorul apasă butonul
    public void OnElementClicked(string skinId, int price, bool owned)
    {
        ShopSkinData skin = skins.Find(s => s.id == skinId);
        if (skin == null) return;

        if (owned)
        {
            SelectSkin(skin);
            return;
        }

        if (levelManager == null)
        {
            Debug.LogWarning("ShopManager needs LevelManager reference for coin logic.");
            return;
        }

        if (levelManager.SpendCoins(price))
        {
            BuySkin(skin);
        }
        else
        {
            // notificare via LevelManager.NotificationManager dacă e legat
            if (levelManager.notificationManager != null)
                levelManager.notificationManager.ShowNotification("Not enough coins", 2f);
        }
    }

    private void BuySkin(ShopSkinData skin)
    {
        _owned.Add(skin.id);
        PlayerPrefs.SetInt("SkinOwned_" + skin.id, 1);
        PlayerPrefs.Save();
        PopulateShop();
        // imediat selectăm cumpăratul
        SelectSkin(skin);
    }

    public void SelectSkin(ShopSkinData skin)
    {
        selectedSkin = skin;
        PlayerPrefs.SetString(SelectedSkinKey, skin.id);
        PlayerPrefs.Save();
        PopulateShop();

        // Aplicăm skin-ul și culoarea săgeții la toate blocurile din scenă
        if (levelManager != null)
        {
            foreach (var block in levelManager.GetActiveBlocks())
            {
                if (block != null)
                {
                    block.ApplySkin(skin.material);
                    block.arrowCollor = skin.arrowColor; // Transmitem culoarea săgeții
                    block.ApplyArrowColor(); // Aplicăm culoarea în shader-ul custom
                }
            }
        }
    }

    /// <summary>
    /// Returnează materialul skin-ului selectat.
    /// </summary>
    public Material selectedMaterial
    {
        get
        {
            return selectedSkin != null ? selectedSkin.material : null;
        }
    }
}
