using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerStats : MonoBehaviour
{

	public IDictionary<Buyables, int> BuyingInventory;

	internal Array BuyablesArray = Enum.GetValues(typeof(Buyables));

	public IDictionary<Buyables, int> Inventory;

	public IList<SolarPanel> SolarPanels;

	public IList<Buildable> EnergyConsumers;

	public IList<OxygenExtractor> OxygenExtractors;

	public IList<OxygenRefinery> OxygenRefineries;

	public IList<MicrowaveAntenna> MicrowaveAntennas;

    public IList<MineralExtractor> MineralExtractors;

	public IList<SolarPanelFactory> SolarPanelsFactory;

	public Robonauta Robonauta;

	public Player Player;

	public int TotalEnergy = 100;

	public int TotalOxygen = 0;

	public int TotalAlluminum = 0;

	public int TotalSilicon = 0;

	public int TotalIron = 0;

	void Start()
	{
		this.BuyingInventory = new Dictionary<Buyables, int>(BuyablesArray.Length);

		this.Inventory = new Dictionary<Buyables, int>(BuyablesArray.Length);

		this.SolarPanels = new List<SolarPanel>();

		this.OxygenExtractors = new List<OxygenExtractor>();

		this.OxygenRefineries = new List<OxygenRefinery>();

		this.MicrowaveAntennas = new List<MicrowaveAntenna>();

        this.MineralExtractors = new List<MineralExtractor>();

		this.SolarPanelsFactory = new List<SolarPanelFactory>();
		
		this.Robonauta = new Robonauta();

		this.EnergyConsumers = new List<Buildable>() { this.Robonauta };

		this.Player = new Player() { Budget = 1000 };

		foreach (var item in BuyablesArray)
		{
			this.BuyingInventory.Add((Buyables)item, 0);

			this.Inventory.Add((Buyables)item, 0);
		}
	}

	public bool Buy(Buyables objectToBuy, int cantidad)
	{
		if (objectToBuy == Buyables.MicrowaveAntenna || objectToBuy == Buyables.OxygenExtractor)
		{
			if (this.BuyingInventory[objectToBuy] >= 1)
			{
				return false;
			}
		}
		bool canBuy = this.Player.HasBudgetToBuy(objectToBuy, cantidad);
		bool hasPrerequisites = this.Player.HasPrerequisites(objectToBuy, cantidad);

		if (canBuy && hasPrerequisites)
		{
			this.BuyingInventory[objectToBuy] += cantidad;

			this.Player.DiscountTotalPrice(objectToBuy, cantidad);
		}

		return canBuy;
	}

	public int GetBuyingAmountOf(Buyables objectToBuy)
	{
		return this.BuyingInventory[objectToBuy];
	}

	public int GetInventoryAmountOf(Buyables objectToBuy)
	{
		return this.Inventory[objectToBuy];
	}

	public void ResetBuyables()
	{
		for (int i = 0; i < BuyablesArray.Length; i++)
			this.BuyingInventory[(Buyables)BuyablesArray.GetValue(i)] = 0;
	}

	public bool HasInInventory(Buyables objectToVerify)
	{
		return this.Inventory[objectToVerify] > 0;
	}

	public void UpdateAmountInInventory(Buyables objectToUpdate, int amount)
	{
		this.Inventory[objectToUpdate] += amount;

		if (this.Inventory[objectToUpdate] < 0)
		{
			this.Inventory[objectToUpdate] = 0;
		}
	}

	public Buildable AddSolarPanel(GameObject clone)
	{
		SolarPanel newSolarPanel = new SolarPanel(clone);
		this.SolarPanels.Add(newSolarPanel);
		newSolarPanel.Id = this.SolarPanels.Count - 1;
		return newSolarPanel;
	}

	public Buildable AddOxygenExtractor(GameObject clone)
	{
		OxygenExtractor newOxygenExtractor = new OxygenExtractor(clone);

		this.OxygenExtractors.Add(newOxygenExtractor);

		this.EnergyConsumers.Add(newOxygenExtractor);
		newOxygenExtractor.Id = this.OxygenExtractors.Count - 1;
		return newOxygenExtractor;
	}

	public void ExtractResources()
	{
		if (this.SolarPanels.Any())
		{
			this.TotalEnergy += this.SolarPanels.Sum(c => c.Energy);
		}
		if (this.OxygenExtractors.Any())
		{
			this.TotalOxygen += this.OxygenExtractors.Sum(c => c.Oxygen);
		}

        if (this.MineralExtractors.Any())
        {
			this.TotalAlluminum += this.MineralExtractors.Sum(c => c.Mineral);
			this.TotalSilicon += this.MineralExtractors.Sum(c => c.Mineral);
			this.TotalIron += this.MineralExtractors.Sum(c => c.Mineral);
        }
	}

	public void ConsumeEnergy()
	{
		this.TotalEnergy -= this.EnergyConsumers.Sum(c => c.GetConsumeEnergyFactor());

		this.TotalEnergy = this.TotalEnergy < 0 ? 0 : this.TotalEnergy;
	}

	public void DegradateObjects()
	{
		if (this.SolarPanels.Any())
		{
			foreach (var item in this.SolarPanels)
			{
				item.Degradate();
			}

			var deadSolarPanels = this.SolarPanels.Where(s => s.IsDead).ToList();

			foreach (var item in deadSolarPanels)
			{
				Destroy(item.gameObject);
			}

			this.SolarPanels = this.SolarPanels.Where(s => !s.IsDead).ToList();
		}
	}

	public void ManageIncomes()
	{
		if (this.MicrowaveAntennas.Any())
		{
			// How much money that energy generates
			this.Player.Budget += this.MicrowaveAntennas.Sum(c => c.GetIncome());
			// Decrease the amount of energy just sold
			this.TotalEnergy -= this.MicrowaveAntennas.Sum(c => c.GetSellFactor());
		}

		if (this.OxygenRefineries.Any())
		{
			this.Player.Budget += this.OxygenRefineries.Sum(c => c.getIncome());
			this.TotalOxygen -= this.OxygenRefineries.Sum(c => c.GetSellFactor());
		}
	}

	public void ExtractOxygen()
	{
		if (OxygenExtractors.Any())
		{
			this.TotalOxygen += this.OxygenExtractors.Sum(c => c.Oxygen);
		}
	}

	public void ConsumeMinerals() 
	{
		if (this.SolarPanelsFactory.Any())
		{
			this.TotalSilicon -= this.SolarPanelsFactory.Sum(c => c.ConsumeSiliconFactor());
			this.TotalAlluminum -= this.SolarPanelsFactory.Sum(c => c.ConsumeAlluminumFactor());
			this.TotalIron -= this.SolarPanelsFactory.Sum(c => c.ConsumeIronFactor());
			this.Inventory[Buyables.SolarPanel] += 1;

			this.TotalSilicon = this.TotalSilicon < 0 ? 0 : this.TotalSilicon;
			this.TotalAlluminum = this.TotalAlluminum < 0 ? 0 : this.TotalAlluminum;
			this.TotalIron = this.TotalIron < 0 ? 0 : this.TotalIron;
		}
	}

	public Buildable AddMicrowaveAntenna(GameObject clone)
	{
		MicrowaveAntenna newMicrowaveAntenna = new MicrowaveAntenna(clone);

		this.MicrowaveAntennas.Add(newMicrowaveAntenna);
		newMicrowaveAntenna.Id = this.MicrowaveAntennas.Count - 1;

		this.EnergyConsumers.Add(newMicrowaveAntenna);

		return newMicrowaveAntenna;
	}

	public Buildable AddSolarPanelFactory(GameObject clone)
	{
		SolarPanelFactory newSolarPanelFactory = new SolarPanelFactory(clone);

		this.SolarPanelsFactory.Add(newSolarPanelFactory);
		newSolarPanelFactory.Id = this.SolarPanelsFactory.Count - 1;

		this.EnergyConsumers.Add(newSolarPanelFactory);

		return newSolarPanelFactory;
	}

	internal void removeGameObject(Buildable buildable)
	{
		if (buildable is SolarPanel)
		{
			Debug.Log("this.SolarPanels.count: " + this.SolarPanels.Count);
			var objectToRemove = this.SolarPanels.SingleOrDefault(c => c.Id == buildable.Id);
			this.SolarPanels.Remove(objectToRemove);
		}
		if (buildable is OxygenExtractor)
		{
			var objectToRemove = this.OxygenExtractors.SingleOrDefault(c => c.Id == buildable.Id);
			this.OxygenExtractors.Remove(objectToRemove);
		}
		if (buildable is MicrowaveAntenna)
		{
			var objectToRemove = this.MicrowaveAntennas.SingleOrDefault(c => c.Id == buildable.Id);
			this.MicrowaveAntennas.Remove(objectToRemove);
		}
		if (buildable is MineralExtractor)
		{
			var objectToRemove = this.MineralExtractors.SingleOrDefault(c => c.Id == buildable.Id);
			this.MineralExtractors.Remove(objectToRemove);
		}
		if (buildable is OxygenRefinery)
		{
			var objectToRemove = this.OxygenRefineries.SingleOrDefault(c => c.Id == buildable.Id);
			this.OxygenRefineries.Remove(objectToRemove);
		}
		if (buildable is SolarPanelFactory)
		{
			var objectToRemove = this.SolarPanelsFactory.SingleOrDefault(c => c.Id == buildable.Id);
			this.SolarPanelsFactory.Remove(objectToRemove);
		}
		var buildableToRemove = this.EnergyConsumers.SingleOrDefault(c => c.Id == buildable.Id && c is MicrowaveAntenna);
		if (buildableToRemove != null)
		{
			this.EnergyConsumers.Remove(buildableToRemove);
		}
		buildableToRemove = this.EnergyConsumers.SingleOrDefault(c => c.Id == buildable.Id && c is OxygenExtractor);
		if (buildableToRemove != null)
		{
			this.EnergyConsumers.Remove(buildableToRemove);
		}

	}
	
    public Buildable AddMineralExtractor(GameObject clone)
    {
        MineralExtractor newMineralExtractor = new MineralExtractor(clone);

        this.MineralExtractors.Add(newMineralExtractor);
        newMineralExtractor.Id = this.MineralExtractors.Count - 1;

        this.EnergyConsumers.Add(newMineralExtractor);

        return newMineralExtractor;
    }

	internal Buildable AddOxygenRefinery(GameObject clone)
	{
		OxygenRefinery newOxygenRefinery = new OxygenRefinery(clone);

		this.OxygenRefineries.Add(newOxygenRefinery);

		this.EnergyConsumers.Add(newOxygenRefinery);
		newOxygenRefinery.Id = this.OxygenRefineries.Count - 1;
		return newOxygenRefinery;
	}

}