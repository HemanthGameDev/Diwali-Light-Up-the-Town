using System;
using System.Collections.Generic;
using DiwaliTown.Economy;
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class CrackerInventory : MonoBehaviour
    {
        [SerializeField] private GoldWallet wallet;

        private readonly Dictionary<CrackerDefinition, int> stock =
            new Dictionary<CrackerDefinition, int>();

        public event Action<CrackerDefinition, int> StockChanged;

        public int GetStock(CrackerDefinition definition)
        {
            if (definition == null)
                return 0;

            return stock.TryGetValue(definition, out int quantity)
                ? quantity
                : 0;
        }

        public int GetBatchCost(CrackerDefinition definition)
        {
            if (definition == null)
                return -1;

            long cost =
                (long)definition.UnitPrice *
                definition.PurchaseBatchSize;

            return cost > 0 && cost <= int.MaxValue
                ? (int)cost
                : -1;
        }

        public bool CanPurchaseBatch(CrackerDefinition definition)
        {
            int cost = GetBatchCost(definition);

            return wallet != null &&
                   cost > 0 &&
                   wallet.CurrentGold >= cost &&
                   GetStock(definition) <=
                       int.MaxValue - definition.PurchaseBatchSize;
        }

        public bool TryPurchaseBatch(CrackerDefinition definition)
        {
            if (definition == null || wallet == null)
                return false;

            int currentStock = GetStock(definition);
            int batchSize = definition.PurchaseBatchSize;
            int totalCost = GetBatchCost(definition);

            if (totalCost <= 0 ||
                currentStock > int.MaxValue - batchSize)
            {
                return false;
            }

            if (!wallet.TrySpend(totalCost))
                return false;

            int newStock = currentStock + batchSize;
            stock[definition] = newStock;

            StockChanged?.Invoke(definition, newStock);
            return true;
        }

        public bool TryConsume(
            CrackerDefinition definition,
            int quantity = 1)
        {
            if (definition == null || quantity <= 0)
                return false;

            int currentStock = GetStock(definition);

            if (currentStock < quantity)
                return false;

            int newStock = currentStock - quantity;
            stock[definition] = newStock;

            StockChanged?.Invoke(definition, newStock);
            return true;
        }

        public void ClearAllStock()
        {
            var existingDefinitions =
                new List<CrackerDefinition>(stock.Keys);

            stock.Clear();

            foreach (CrackerDefinition definition in existingDefinitions)
            {
                StockChanged?.Invoke(definition, 0);
            }
        }

        public bool RestoreStock(
            CrackerDefinition definition,
            int quantity)
        {
            if (definition == null || quantity < 0)
            {
                return false;
            }

            if (quantity == 0)
            {
                stock.Remove(definition);
            }
            else
            {
                stock[definition] = quantity;
            }

            StockChanged?.Invoke(definition, quantity);
            return true;
        }
    }
}