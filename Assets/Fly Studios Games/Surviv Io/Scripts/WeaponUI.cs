using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponUI : MonoBehaviour
{
	[Header("References")]
	public WeaponControler weaponController; // assign in Inspector (sau Find)

	[Header("Update")]
	[Tooltip("Cât de des actualizăm UI (s) pentru a nu pune presiune la Update()")]
	public float refreshInterval = 0.15f;

    [Space]
    public List<WeaponIventorySlotUI> weaponIventorySlotUIs;
	[Space]
	public Text currentAmoInSelectedWeapon;
	public Text currentTotalAmoInSelectedWeapon;
public GameObject weaponCurrentAmoPanel;
	private Coroutine _updateRoutine;

	// INVENTORY STATE (aliniat cu weaponIventorySlotUIs)
	private class WeaponEntry { public WeaponData data; public int mag; public int reserve; }
	private List<WeaponEntry> _entries = new List<WeaponEntry>();
	private int _selectedIndex = -1;

	// Pending ammo bank
	private Dictionary<WeaponData, int> _pendingAmmoBank = new Dictionary<WeaponData, int>();
	private int _pendingUniversalAmmo = 0;

	private void Reset()
	{
		if (weaponController == null)
			weaponController = FindObjectOfType<WeaponControler>();
	}

	private void OnEnable()
	{
		_updateRoutine = StartCoroutine(RefreshLoop());

		// inițializează lista de entries la dimensiunea sloturilor
		EnsureInventorySize();

		// dacă există deja o armă echipată la start, asigură-te că apare în inventory
		if (weaponController != null && weaponController.CurrentWeapon != null)
		{
			var w = weaponController.CurrentWeapon;
			if (FindIndexOf(w) == -1)
			{
				(int mag, int reserve, int _) = weaponController.GetAmmoInfo();
				AddWeaponInternal(w, mag, reserve, select: true, preferIndex: 0);
				Debug.Log($"[WeaponUI] Added starting weapon '{w.weaponName}' into slot 1.");
			}
			else
			{
				_selectedIndex = Mathf.Max(0, FindIndexOf(w));
				// aplicăm pending ammo dacă există
				TryApplyPendingToIndex(_selectedIndex);
			}
		}
		if (weaponCurrentAmoPanel != null) weaponCurrentAmoPanel.SetActive(false);
	}

	private void OnDisable()
	{
		if (_updateRoutine != null) StopCoroutine(_updateRoutine);
	}

	private void Update()
	{
		// select via numeric keys 1..N
		if (weaponIventorySlotUIs != null && weaponIventorySlotUIs.Count > 0)
		{
			for (int i = 0; i < weaponIventorySlotUIs.Count && i < 9; i++)
			{
				if (Input.GetKeyDown(KeyCode.Alpha1 + i))
				{
					SelectSlotIndex(i);
				}
			}
			// 0 key maps to index 9 (10th slot)
			if (weaponIventorySlotUIs.Count >= 10 && Input.GetKeyDown(KeyCode.Alpha0))
			{
				SelectSlotIndex(9);
			}
		}

		// NEW: Drop current selected weapon and switch to hands
		if (Input.GetKeyDown(KeyCode.X))
		{
			DropSelectedWeapon();
		}
	}

	private IEnumerator RefreshLoop()
	{
		while (true)
		{
			RefreshUI();
			yield return new WaitForSeconds(refreshInterval);
		}
	}

	private void RefreshUI()
	{
		if (weaponController == null) return;

		// sync ammo of selected entry from controller
		if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
		{
			var sel = _entries[_selectedIndex];
			if (sel != null && sel.data != null && weaponController.CurrentWeapon == sel.data)
			{
				var info = weaponController.GetAmmoInfo();
				sel.mag = info.mag;
				sel.reserve = info.reserve;

				// current ammo texts
				if (currentAmoInSelectedWeapon != null) currentAmoInSelectedWeapon.text = sel.mag.ToString();
				// Show only reserve so it updates on reload/pickups, not on each shot
				if (currentTotalAmoInSelectedWeapon != null) currentTotalAmoInSelectedWeapon.text = sel.reserve.ToString();
			}
		}

		// refresh slot UIs
		RefreshSlotsUI();

		// ammo panel visibility control
		bool hasSelectedWeapon =
			_selectedIndex >= 0 &&
			_selectedIndex < _entries.Count &&
			_entries[_selectedIndex] != null &&
			_entries[_selectedIndex].data != null &&
			weaponController != null &&
			weaponController.CurrentWeapon == _entries[_selectedIndex].data;

		if (weaponCurrentAmoPanel != null)
		{
			weaponCurrentAmoPanel.SetActive(hasSelectedWeapon);
			if (!hasSelectedWeapon)
			{
				if (currentAmoInSelectedWeapon != null) currentAmoInSelectedWeapon.text = "";
				if (currentTotalAmoInSelectedWeapon != null) currentTotalAmoInSelectedWeapon.text = "";
			}
		}
	}

	private void RefreshSlotsUI()
	{
		EnsureInventorySize();
		for (int i = 0; i < weaponIventorySlotUIs.Count; i++)
		{
			var slotUI = weaponIventorySlotUIs[i];
			if (slotUI == null) continue;

			var entry = _entries[i];
			Sprite icon = entry?.data != null ? entry.data.weaponSpriteIcon : null;
			string name = entry?.data != null ? entry.data.weaponName : "";
			int total = entry != null ? (entry.mag + entry.reserve) : 0;
			bool selected = (i == _selectedIndex);

			slotUI.RefreshSlot(icon, i + 1, total, name, selected);
		}
	}

	private void EnsureInventorySize()
	{
		if (weaponIventorySlotUIs == null) return;
		int need = weaponIventorySlotUIs.Count;
		while (_entries.Count < need) _entries.Add(null);
		while (_entries.Count > need) _entries.RemoveAt(_entries.Count - 1);
		if (_selectedIndex >= _entries.Count) _selectedIndex = _entries.Count - 1;
	}

	private int FindFirstEmpty()
	{
		for (int i = 0; i < _entries.Count; i++)
			if (_entries[i] == null || _entries[i]?.data == null) return i;
		return -1;
	}

	private int FindIndexOf(WeaponData data)
	{
		if (data == null) return -1;
		for (int i = 0; i < _entries.Count; i++)
			if (_entries[i]?.data == data) return i;
		return -1;
	}

	private (int mag, int reserve) ComputeInitialAmmo(WeaponData d)
	{
		if (d == null) return (0, 0);
		int mag = Mathf.Min(d.magazineSize, d.startingReserveAmmo);
		int reserve = Mathf.Clamp(d.startingReserveAmmo - mag, 0, d.maxReserveAmmo);
		return (mag, reserve);
	}

	// Public API for pickups to add weapon to inventory
	public void AddWeaponToInventory(WeaponData data, bool select = true)
	{
		if (data == null) return;
		EnsureInventorySize();

		// already have it? just select (și aplicăm pending)
		int existing = FindIndexOf(data);
		if (existing != -1)
		{
			// aplică pending ammo întâi
			TryApplyPendingToIndex(existing);
			if (select) SelectSlotIndex(existing);
			Debug.Log($"[WeaponUI] Weapon '{data.weaponName}' already in slot {existing + 1}. Selected={select}");
			return;
		}

		var init = ComputeInitialAmmo(data);
		AddWeaponInternal(data, init.mag, init.reserve, select, preferIndex: FindFirstEmpty());
	}

	private void AddWeaponInternal(WeaponData data, int mag, int reserve, bool select, int preferIndex)
	{
		int idx = preferIndex;
		if (idx < 0) idx = (_selectedIndex >= 0 ? _selectedIndex : 0); // replace current if full

		_entries[idx] = new WeaponEntry { data = data, mag = mag, reserve = reserve };

		// Aplicăm pending ammo (inclusiv universal) pe acest entry
		int added = TryApplyPendingToIndex(idx);
		Debug.Log($"[WeaponUI] Put weapon '{data.weaponName}' into slot {idx + 1} (mag={mag}, reserve={reserve} + pending {added}).");

		if (select)
		{
			SelectSlotIndex(idx);
		}
		else
		{
			RefreshSlotsUI();
		}
	}

	private void SelectSlotIndex(int idx)
	{
		if (idx < 0 || idx >= _entries.Count) return;

		var entry = _entries[idx];

		// If slot is empty -> switch to hands (unequip) and mark no selection
		if (entry == null || entry.data == null)
		{
			// save current selected ammo back into its entry as before
			if (_selectedIndex >= 0 && _selectedIndex < _entries.Count && weaponController != null)
			{
				var curEntry = _entries[_selectedIndex];
				if (curEntry != null && curEntry.data != null && weaponController.CurrentWeapon == curEntry.data)
				{
					var info = weaponController.GetAmmoInfo();
					curEntry.mag = info.mag;
					curEntry.reserve = info.reserve;
				}
			}

			// clear selection and equip hands
			_selectedIndex = -1;
			if (weaponController != null)
				weaponController.EquipWeapon(null); // equips melee / hands
			RefreshUI();
			return;
		}

		// save current selected ammo back into its entry
		if (_selectedIndex >= 0 && _selectedIndex < _entries.Count && weaponController != null)
		{
			var curEntry = _entries[_selectedIndex];
			if (curEntry != null && curEntry.data != null && weaponController.CurrentWeapon == curEntry.data)
			{
				var info = weaponController.GetAmmoInfo();
				curEntry.mag = info.mag;
				curEntry.reserve = info.reserve;
			}
		}

		_selectedIndex = idx;

		// Înainte de echipare, aplicăm pending ammo pe entry-ul selectat
		int addedFromPending = TryApplyPendingToIndex(idx);

		// equip selected with stored ammo
		if (weaponController != null)
		{
			weaponController.EquipWeapon(entry.data, entry.mag, entry.reserve);
			if (addedFromPending > 0)
			{
				var info = weaponController.GetAmmoInfo();
				entry.mag = info.mag;
				entry.reserve = info.reserve;
			}
			Debug.Log($"[WeaponUI] Selected slot {idx + 1}: '{entry.data.weaponName}' (mag={entry.mag}, reserve={entry.reserve}, +pending {addedFromPending}).");
		}

		RefreshUI();
	}

	// NEW: drop currently selected weapon (remove from inventory and equip hands)
	private void DropSelectedWeapon()
	{
		// If no selected weapon -> ensure hands are active
		if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
		{
			if (weaponController != null) weaponController.EquipWeapon(null);
			_selectedIndex = -1;
			RefreshUI();
			return;
		}

		var entry = _entries[_selectedIndex];
		if (entry == null || entry.data == null)
		{
			// empty slot -> just equip hands
			if (weaponController != null) weaponController.EquipWeapon(null);
			_selectedIndex = -1;
			RefreshUI();
			return;
		}

		// If weapon currently equipped -> unequip via EquipWeapon(null)
		if (weaponController != null && weaponController.CurrentWeapon == entry.data)
		{
			weaponController.EquipWeapon(null);
		}

		// Remove from inventory
		Debug.Log($"[WeaponUI] Dropped weapon '{entry.data.weaponName}' from slot {_selectedIndex + 1}.");
		_entries[_selectedIndex] = null;

		// After drop, switch to hands (no selected weapon)
		_selectedIndex = -1;
		if (weaponController != null) weaponController.EquipWeapon(null);

		RefreshUI();
	}

	// Colectare ammo din pickup (poate fi pentru arme pe care nu le avem încă)
	public void CollectAmmo(BulletAmmoData ammoData)
	{
		if (ammoData == null) return;
		int amount = Mathf.Max(0, ammoData.ammoCount);
		if (amount <= 0) return;

		EnsureInventorySize();

		// 1) universal ammo (compatibil cu orice armă)
		if (ammoData.compatibleWeapons == null || ammoData.compatibleWeapons.Count == 0)
		{
			// dacă arma selectată există -> aplicăm direct
			if (_selectedIndex >= 0 && _selectedIndex < _entries.Count && _entries[_selectedIndex]?.data != null)
			{
				AddAmmoToEntry(_selectedIndex, amount, applyToControllerIfSelected: true);
			}
			else
			{
				_pendingUniversalAmmo += amount;
			}
			RefreshUI();
			return;
		}

		// 2) ammo pentru o listă de arme specifice
		// preferăm slotul selectat dacă este compatibil
		if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
		{
			var sel = _entries[_selectedIndex];
			if (sel != null && sel.data != null && ammoData.compatibleWeapons.Contains(sel.data))
			{
				AddAmmoToEntry(_selectedIndex, amount, applyToControllerIfSelected: true);
				RefreshUI();
				return;
			}
		}

		// 3) găsim orice armă compatibilă deja deținută
		for (int i = 0; i < _entries.Count; i++)
		{
			var e = _entries[i];
			if (e != null && e.data != null && ammoData.compatibleWeapons.Contains(e.data))
			{
				bool applyNow = (i == _selectedIndex);
				AddAmmoToEntry(i, amount, applyToControllerIfSelected: applyNow);
				RefreshUI();
				return;
			}
		}

		// 4) nu deținem încă arme compatibile -> stocăm pe prima din listă în pending
		var target = ammoData.compatibleWeapons[0];
		if (target != null)
		{
			if (_pendingAmmoBank.ContainsKey(target)) _pendingAmmoBank[target] += amount;
			else _pendingAmmoBank[target] = amount;
			Debug.Log($"[WeaponUI] Stored {amount} pending ammo for '{target.weaponName}'.");
		}

		RefreshUI();
	}

	// Adaugă ammo la entry; dacă este slotul selectat și arma e echipată, împinge în WeaponControler
	private void AddAmmoToEntry(int idx, int amount, bool applyToControllerIfSelected)
	{
		if (idx < 0 || idx >= _entries.Count) return;
		var entry = _entries[idx];
		if (entry == null || entry.data == null || amount <= 0) return;

		// dacă slotul este selectat și arma echipată -> adaugă prin controller pentru a păstra sincron
		if (applyToControllerIfSelected && weaponController != null && weaponController.CurrentWeapon == entry.data)
		{
			int added = weaponController.AddAmmo(amount);
			// sync înapoi în entry
			var info = weaponController.GetAmmoInfo();
			entry.mag = info.mag;
			entry.reserve = info.reserve;

			// overflow (dacă nu s-a adăugat tot) -> pending pe arma asta
			int overflow = amount - Mathf.Max(0, added);
			if (overflow > 0)
			{
				if (_pendingAmmoBank.ContainsKey(entry.data)) _pendingAmmoBank[entry.data] += overflow;
				else _pendingAmmoBank[entry.data] = overflow;
			}
			return;
		}

		// Non-selectat: creștem doar reserve în entry, până la limită
		int maxReserve = entry.data.maxReserveAmmo;
		int newReserve = Mathf.Clamp(entry.reserve + amount, 0, maxReserve);
		int applied = newReserve - entry.reserve;
		entry.reserve = newReserve;

		// overflow -> pending pe arma asta
		int overflowNonSel = amount - Mathf.Max(0, applied);
		if (overflowNonSel > 0)
		{
			if (_pendingAmmoBank.ContainsKey(entry.data)) _pendingAmmoBank[entry.data] += overflowNonSel;
			else _pendingAmmoBank[entry.data] = overflowNonSel;
		}
	}

	// Aplică pending ammo (arma specifică + universal) pe entry-ul dat; returnează cât s-a adăugat în reserve
	private int TryApplyPendingToIndex(int idx)
	{
		if (idx < 0 || idx >= _entries.Count) return 0;
		var entry = _entries[idx];
		if (entry == null || entry.data == null) return 0;

		int addedTotal = 0;

		// 1) pending specific pentru arma aceasta
		int pendingForWeapon = 0;
		_pendingAmmoBank.TryGetValue(entry.data, out pendingForWeapon);

		// 2) universal pending
		int pendingUniversal = _pendingUniversalAmmo;

		// Capacitate rămasă în rezervă
		int capacity = Mathf.Max(0, entry.data.maxReserveAmmo - entry.reserve);
		if (capacity <= 0)
		{
			return 0;
		}

		// Aplicăm întâi pending specific
		int takeFromWeapon = Mathf.Min(capacity, pendingForWeapon);
		entry.reserve += takeFromWeapon;
		addedTotal += takeFromWeapon;
		capacity -= takeFromWeapon;
		if (takeFromWeapon > 0)
		{
			_pendingAmmoBank[entry.data] = pendingForWeapon - takeFromWeapon;
			if (_pendingAmmoBank[entry.data] <= 0) _pendingAmmoBank.Remove(entry.data);
		}

		// Apoi universal
		if (capacity > 0 && pendingUniversal > 0)
		{
			int takeFromUniversal = Mathf.Min(capacity, pendingUniversal);
			entry.reserve += takeFromUniversal;
			addedTotal += takeFromUniversal;
			_pendingUniversalAmmo -= takeFromUniversal;
		}

		// dacă slotul este selectat și arma echipată, împingem în controller
		if (addedTotal > 0 && weaponController != null && _selectedIndex == idx && weaponController.CurrentWeapon == entry.data)
		{
			int actuallyAdded = weaponController.AddAmmo(addedTotal);
			// sync înapoi în entry
			var info = weaponController.GetAmmoInfo();
			entry.mag = info.mag;
			entry.reserve = info.reserve;

			// dacă nu s-a putut adăuga tot în controller (unlikely, dar protejăm), reîntoarcem surplusul în pending
			int overflow = addedTotal - Mathf.Max(0, actuallyAdded);
			if (overflow > 0)
			{
				if (_pendingAmmoBank.ContainsKey(entry.data)) _pendingAmmoBank[entry.data] += overflow;
				else _pendingAmmoBank[entry.data] = overflow;
			}
		}

		return addedTotal;
	}
}
