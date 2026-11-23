using System;
using System.Collections.Generic;
using Gssoft.Gscad.ApplicationServices;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Helpers
{
    /// <summary>
    /// Cache TankShellLayout trong RAM, khóa theo Document.
    /// Ghi bởi TAOTHANTANK sau khi vẽ; lệnh khác (vd. PART_CHOM) đọc neo đầu/cuối theo view.
    /// Không ghi vào file DWG — xóa khi đóng document.
    /// </summary>
    public static class TankShellGeometryCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<Document, TankShellLayout> ByDocument =
            new Dictionary<Document, TankShellLayout>();
        private static readonly HashSet<Document> HookedDocuments = new HashSet<Document>();

        /// <summary>
        /// Lưu bản clone layout cho document hiện tại.
        /// </summary>
        public static void Set(Document doc, TankShellLayout layout)
        {
            if (doc == null || layout == null)
            {
                return;
            }

            lock (Sync)
            {
                EnsureCloseHook(doc);
                ByDocument[doc] = layout.Clone();
            }
        }

        /// <summary>
        /// Đọc layout đã cache. Trả về clone để không sửa trực tiếp dữ liệu trong cache.
        /// </summary>
        public static bool TryGet(Document doc, out TankShellLayout layout)
        {
            layout = null;
            if (doc == null)
            {
                return false;
            }

            lock (Sync)
            {
                TankShellLayout cached;
                if (!ByDocument.TryGetValue(doc, out cached) || cached == null)
                {
                    return false;
                }

                layout = cached.Clone();
                return true;
            }
        }

        /// <summary>
        /// Xóa entry của document (khi đóng bản vẽ hoặc gọi thủ công).
        /// </summary>
        public static void Remove(Document doc)
        {
            if (doc == null)
            {
                return;
            }

            lock (Sync)
            {
                ByDocument.Remove(doc);
                if (HookedDocuments.Remove(doc))
                {
                    doc.BeginDocumentClose -= OnBeginDocumentClose;
                }
            }
        }

        private static void EnsureCloseHook(Document doc)
        {
            if (HookedDocuments.Contains(doc))
            {
                return;
            }

            doc.BeginDocumentClose += OnBeginDocumentClose;
            HookedDocuments.Add(doc);
        }

        private static void OnBeginDocumentClose(object sender, DocumentBeginCloseEventArgs e)
        {
            var doc = sender as Document;
            if (doc == null)
            {
                return;
            }

            lock (Sync)
            {
                ByDocument.Remove(doc);
                if (HookedDocuments.Remove(doc))
                {
                    doc.BeginDocumentClose -= OnBeginDocumentClose;
                }
            }
        }
    }
}
