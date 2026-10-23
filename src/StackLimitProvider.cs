using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ItemStatsSystem;
using UnityEngine;

namespace SplitRationalizer
{
    /// <summary>
    /// 读取「原版」堆叠上限。
    ///
    /// 关键点：不能直接用 Item.MaxStackCount 或 ItemMetaData(Item)。
    /// 其他堆叠 mod（例如鸭窝扩容）会 Harmony patch MaxStackCount 的 getter，
    /// 动态物品的 MetaData 又是通过 getter 构造的，读到的都是被放大后的值。
    /// 原版物品的 ItemAssetsCollection.Entry.metaData 是构建时序列化的原始数据，
    /// 这里优先用它；动态物品（其他 mod 新增）则反射读取私有的 maxStackCount 字段，
    /// 绕过任何 getter 补丁。
    /// </summary>
    internal static class StackLimitProvider
    {
        public const string DumpFileName = "vanilla_stack_limits.csv";

        private static readonly FieldInfo RawMaxStackField =
            typeof(Item).GetField("maxStackCount", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Dictionary<int, int> _vanillaLimits;

        /// <summary>返回该物品的原版堆叠上限；查不到时退回当前运行时值。</summary>
        public static int GetVanillaMaxStack(Item item)
        {
            if (item == null)
                return 0;

            try
            {
                if (_vanillaLimits == null)
                    BuildCache();

                if (_vanillaLimits != null && _vanillaLimits.TryGetValue(item.TypeID, out int vanilla) && vanilla > 0)
                    return vanilla;

                int raw = ReadRawMaxStack(item);
                if (raw > 0)
                    return raw;

                return item.MaxStackCount;
            }
            catch (Exception e)
            {
                SafeGuard.Report("读取物品的原版叠加上限失败", e);

                // 0 表示“读取失败”，由调用方提示错误；
                // 不能用 1 冒充合法值，否则会悄悄把拆分数量改成 1
                return 0;
            }
        }

        private static void BuildCache()
        {
            Dictionary<int, int> limits = new Dictionary<int, int>();
            bool built = false;

            try
            {
                ItemAssetsCollection collection = ItemAssetsCollection.Instance;
                if (collection != null && collection.entries != null)
                {
                    foreach (ItemAssetsCollection.Entry entry in collection.entries)
                    {
                        if (entry == null)
                            continue;

                        int max = entry.metaData.maxStackCount;
                        if (max <= 0 && entry.prefab != null)
                            max = ReadRawMaxStack(entry.prefab);
                        if (max > 0)
                            limits[entry.typeID] = max;
                    }
                    built = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[拆分合理化] 构建原版叠加上限缓存失败：{e}");
            }

            if (built && limits.Count > 0)
            {
                _vanillaLimits = limits;
                Debug.Log($"[拆分合理化] 已缓存 {limits.Count} 条本体物品叠加上限");
            }
            else
            {
                // 失败时不要把空字典缓存下来，否则整个进程都不会再重试
                Debug.LogWarning("[拆分合理化] 原版叠加上限缓存未构建成功，下次点击时重试");
            }
        }

        /// <summary>反射读取私有字段，绕过其他 mod 对 MaxStackCount getter 的修改。</summary>
        private static int ReadRawMaxStack(Item item)
        {
            if (item == null || RawMaxStackField == null)
                return 0;
            try
            {
                object value = RawMaxStackField.GetValue(item);
                return value is int i ? i : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>把所有本体物品的堆叠上限导出为 CSV，返回条目数。</summary>
        public static int DumpVanillaStackLimits(string directory)
        {
            ItemAssetsCollection collection = ItemAssetsCollection.Instance;
            if (collection == null || collection.entries == null)
                throw new InvalidOperationException("ItemAssetsCollection 尚未初始化");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("typeID,name,displayName,displayNameKey,maxStackCount,defaultStackCount,stackable");

            int count = 0;
            foreach (ItemAssetsCollection.Entry entry in collection.entries.OrderBy(e => e == null ? 0 : e.typeID))
            {
                if (entry == null)
                    continue;

                ItemMetaData meta = entry.metaData;
                int max = meta.maxStackCount;
                if (max <= 0 && entry.prefab != null)
                    max = ReadRawMaxStack(entry.prefab);

                string displayName = SafeDisplayName(meta, entry.prefab);

                sb.Append(entry.typeID).Append(',')
                  .Append(Escape(meta.Name)).Append(',')
                  .Append(Escape(displayName)).Append(',')
                  .Append(Escape(meta.DisplayNameKey)).Append(',')
                  .Append(max).Append(',')
                  .Append(meta.defaultStackCount).Append(',')
                  .Append(max > 1 ? "True" : "False")
                  .AppendLine();
                count++;
            }

            string path = Path.Combine(directory, DumpFileName);
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception)
            {
                // mod 目录可能不可写（例如创意工坊目录权限受限），退到持久化目录
                path = Path.Combine(Application.persistentDataPath, DumpFileName);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            }

            Debug.Log($"[拆分合理化] 本体物品叠加上限已导出（{count} 条）：{path}");
            return count;
        }

        private static string SafeDisplayName(ItemMetaData meta, Item prefab)
        {
            try
            {
                string text = meta.DisplayName;
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("*"))
                    return text;
            }
            catch
            {
                // 本地化尚未就绪时忽略
            }

            try
            {
                return prefab != null ? prefab.DisplayName : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            bool needQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needQuote)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
