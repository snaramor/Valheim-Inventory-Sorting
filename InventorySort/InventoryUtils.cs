using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using System.Reflection;
using UnityEngine;

namespace InventorySort
{
    public static class InventoryUtils
    {
        public static bool ShouldSortItem(Vector2i itemPos, Vector2i offset)
        {
            // There appears to be no need to check if a slot is from EquipmentAndQuickslots anymore as of at least v2.1.1.0. It must override methods in the Inventory class?
            // The "isQuickSlot" and "isEquipmentSlot" methods don't even exist anymore as far as I can tell, it was throwing an exception trying to invoke them.
            return itemPos.y > offset.y || (itemPos.y == offset.y && itemPos.x >= offset.x);
        }

        public static void Sort(Inventory inventory, int offset = 0, bool autoStack = false)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var offsetv = new Vector2i(offset % inventory.GetWidth(), offset / inventory.GetWidth());
            
            IEnumerable<ItemDrop.ItemData> toBeSorted = inventory.GetAllItems().Where(itm => ShouldSortItem(itm.m_gridPos, offsetv)).OrderBy((itm) => itm.m_shared.m_name);
            if (Plugin.instance.ShouldAutoStack.Value) {
                IEnumerable<List<ItemDrop.ItemData>> grouped = toBeSorted.Where(itm => itm.m_stack < itm.m_shared.m_maxStackSize).GroupBy(itm => itm.m_shared.m_name).Where(itm => itm.Count() > 1).Select(grouping => grouping.ToList());
                Plugin.instance.GetLogger().LogInfo($"There are {grouped.Count()} groups of stackable items");
                foreach (List<ItemDrop.ItemData> nonFullStacks in grouped)
                {
                    if (nonFullStacks.Count == 0)
                        continue;
                    var maxStack = nonFullStacks.First().m_shared.m_maxStackSize;

                    var numTimes = 0;
                    var curStack = nonFullStacks[0];
                    nonFullStacks.RemoveAt(0);

                    var enumerator = nonFullStacks.GetEnumerator();
                    while (nonFullStacks.Count >= 1)
                    {
                        numTimes += 1;
                        enumerator.MoveNext();
                        var stack = enumerator.Current;
                        if(stack == null)
                            break;

                        if (curStack.m_stack >= maxStack)
                        {
                            curStack = stack;
                            nonFullStacks.Remove(stack);
                            enumerator = nonFullStacks.GetEnumerator();
                            continue;
                        }

                        var toStack = Math.Min(maxStack - curStack.m_stack, stack.m_stack);
                        if (toStack > 0)
                        {
                            curStack.m_stack += toStack;
                            stack.m_stack -= toStack;

                            if (stack.m_stack <= 0)
                            {
                                inventory.RemoveItem(stack);
                            }
                        }
                    }
                    Plugin.instance.GetLogger().LogDebug($"Auto-Stacked in {numTimes} iterations");
                }
            }

            foreach (var item in toBeSorted)
            {
                var x = offset % inventory.GetWidth();
                var y = offset / inventory.GetWidth();
                item.m_gridPos = new Vector2i(x, y);
                offset++;
            }
            sw.Stop();
            Plugin.instance.GetLogger().LogInfo($"Sorting inventory took {sw.Elapsed}");

            //Was throwing a null error on this Invoke. Replacing with m_onchanged.Invoke() seems to have done the trick.
            //typeof(Inventory).GetMethod("Changed").Invoke(inventory, new object[0]);
            inventory.m_onChanged.Invoke();
        }
    }

    public static class ContainerUtils
    {
        public static void Sort(Container container, int offset = 0)
        {
            InventoryUtils.Sort(container.GetInventory(), offset);
        }
    }
}
