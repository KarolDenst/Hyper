﻿using Common.UserInput;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Player.InventorySystem.InventoryRendering;
using Player.InventorySystem.Items;
using Player.InventorySystem.Items.Tools;

namespace Player.InventorySystem;

public class Inventory : IInputSubscriber
{
    private static readonly Lazy<Inventory> _instance = new(() => new Inventory(true));
    public static Inventory Instance => _instance.Value;

    public const int Columns = 10;

    public const int Rows = 4;

    public int SelectedItemIndex = 0;

    public bool IsOpen = false;

    public readonly (Item? Item, int Count)[,] Items;

    public (Item? Item, int Count)[] Hotbar => Enumerable.Range(0, Columns).Select(i => Items[i, 0]).ToArray();

    public Item? SelectedItem => Items[0, SelectedItemIndex].Item;

    public (Item? Item, int Count) InHandItem;

    public Inventory(bool starterItems = false)
    {
        Items = new (Item? Item, int Count)[Columns, Rows];
        RegisterCallbacks();

        if (starterItems)
        {
            for (int i = 0; i < 12; i++)
            {
                AddItem(new Sword());
            }
            for (int i = 0; i < 3; i++)
            {
                AddItem(new Hammer());
            }
            AddItem(new Rock(), 23);
            AddItem(new Rock(), 7);
        }
    }

    public void UseItem() => SelectedItem?.Use();

    public void AddItem(Item item, int count = 1)
    {
        int x, y;
        if (item.IsStackable)
        {
            bool hasItem = TryGetFirstSlotWithItem(item, out x, out y);
            if (hasItem)
            {
                Items[x, y].Count++;
                return;
            }
        }

        bool isEmptySlot = TryGetFirstEmptySlot(out x, out y);
        if (isEmptySlot) Items[x, y] = (item, count);
    }

    private bool TryGetFirstEmptySlot(out int x, out int y)
    {
        for (int j = 0; j < Rows; j++)
        {
            for (int i = 0; i < Columns; i++)
            {
                if (Items[i, j].Item is null)
                {
                    (x, y) = (i, j);
                    return true;
                }
            }
        }

        (x, y) = (-1, -1);
        return false;
    }

    private bool TryGetFirstSlotWithItem(Item item, out int x, out int y)
    {
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (Items[i, j].Item == item)
                {
                    (x, y) = (i, j);
                    return true;
                }
            }
        }

        (x, y) = (-1, -1);
        return false;
    }

    public void DropItem()
    {
        InHandItem.Item = null;
        InHandItem.Count = 0;
    }

    private (Item?, int) RemoveItem(int x, int y)
    {
        var item = Items[x, y];
        Items[x, y] = (null, 0);

        return item;
    }

    public void SwapWithHand(int x, int y)
    {
        if (InHandItem.Item is not null && Items[x, y].Item is not null)
        {
            if (InHandItem.Item.IsStackable)
            {
                if (InHandItem.Item.ID == Items[x, y].Item.ID)
                {
                    Items[x, y].Count += InHandItem.Count;
                    InHandItem.Item = null;
                    InHandItem.Count = 0;
                    return;
                }
            }
        }
        var temp = InHandItem;
        InHandItem = RemoveItem(x, y);
        Items[x, y] = temp;
    }

    public bool TryGetPosition(out int x, out int y, Vector2 mousePosition)
    {
        if (TryGetHotbarPosition(mousePosition, out x, out y)) return true;
        if (TryGetInventoryPosition(mousePosition, out x, out y)) return true;
        return false;
    }

    private bool TryGetHotbarPosition(Vector2 mousePosition, out int x, out int y)
    {
        float cellSize = 2 * InventoryHudManager.HotbarSizeY;

        float gridTopLeftX = InventoryHudManager.HotbarPosition.X - (cellSize * Columns / 2);
        float gridTopLeftY = InventoryHudManager.HotbarPosition.Y - (cellSize / 2);

        x = (int)((mousePosition.X - gridTopLeftX) / cellSize);
        y = (int)((mousePosition.Y - gridTopLeftY) / cellSize);

        return x is >= 0 and < Columns && y == 0;
    }

    private bool TryGetInventoryPosition(Vector2 mousePosition, out int x, out int y)
    {
        float cellSize = 2 * InventoryHudManager.HotbarSizeY;

        float gridTopLeftX = InventoryHudManager.InventoryPosition.X - (cellSize * Columns / 2);
        float gridTopLeftY = InventoryHudManager.InventoryPosition.Y - (cellSize * (Rows - 1) / 2);

        x = (int)((mousePosition.X - gridTopLeftX) / cellSize);
        y = Rows - (int)((mousePosition.Y - gridTopLeftY) / cellSize) - 1;

        return x is >= 0 and < Columns && y is > 0 and < Rows;
    }

    public void RegisterCallbacks()
    {
        Context context = Context.Instance;

        context.RegisterKeyDownCallback(Keys.D0, () => SelectedItemIndex = 9);
        context.RegisterKeyDownCallback(Keys.D1, () => SelectedItemIndex = 0);
        context.RegisterKeyDownCallback(Keys.D2, () => SelectedItemIndex = 1);
        context.RegisterKeyDownCallback(Keys.D3, () => SelectedItemIndex = 2);
        context.RegisterKeyDownCallback(Keys.D4, () => SelectedItemIndex = 3);
        context.RegisterKeyDownCallback(Keys.D5, () => SelectedItemIndex = 4);
        context.RegisterKeyDownCallback(Keys.D6, () => SelectedItemIndex = 5);
        context.RegisterKeyDownCallback(Keys.D7, () => SelectedItemIndex = 6);
        context.RegisterKeyDownCallback(Keys.D8, () => SelectedItemIndex = 7);
    }
}