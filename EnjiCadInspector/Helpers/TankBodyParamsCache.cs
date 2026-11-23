using System;
using System.Collections.Generic;
using Gssoft.Gscad.ApplicationServices;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Helpers
{
    /// <summary>
    /// Cache TankBodyParams trong RAM, khóa theo từng Document CAD.
    /// Giữ được qua các lệnh trong cùng phiên bản vẽ; xóa khi đóng document.
    /// Không ghi vào file DWG.
    /// </summary>
    public static class TankBodyParamsCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<Document, TankBodyParams> ByDocument =
            new Dictionary<Document, TankBodyParams>();
        private static readonly HashSet<Document> HookedDocuments = new HashSet<Document>();

        /// <summary>
        /// Lưu bản clone của <paramref name="parameters"/> cho document hiện tại.
        /// </summary>
        public static void Set(Document doc, TankBodyParams parameters)
        {
            if (doc == null || parameters == null)
            {
                return;
            }

            lock (Sync)
            {
                EnsureCloseHook(doc);
                ByDocument[doc] = parameters.Clone();
            }
        }

        /// <summary>
        /// Đọc params đã cache. Trả về clone để không sửa trực tiếp dữ liệu trong cache.
        /// </summary>
        public static bool TryGet(Document doc, out TankBodyParams parameters)
        {
            parameters = null;
            if (doc == null)
            {
                return false;
            }

            lock (Sync)
            {
                TankBodyParams cached;
                if (!ByDocument.TryGetValue(doc, out cached) || cached == null)
                {
                    return false;
                }

                parameters = cached.Clone();
                return true;
            }
        }

        /// <summary>
        /// Có cache thì trả clone; không có thì tạo giá trị mặc định mới.
        /// </summary>
        public static TankBodyParams GetOrDefaults(Document doc)
        {
            TankBodyParams cached;
            if (TryGet(doc, out cached))
            {
                return cached;
            }

            return TankBodyParams.CreateDefaults();
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

        /// <summary>
        /// Đăng ký BeginDocumentClose một lần / document để cache không giữ bản vẽ đã đóng.
        /// </summary>
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

            // Xóa trong lock; hủy hook trong cùng bước để tránh race khi đóng.
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
