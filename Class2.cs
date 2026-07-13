using Gssoft.Gscad.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace check
{
    public class Class2
    {
        public static void ReportAsm(Editor ed, string name)
        {
            var loaded = Array.Find(
                AppDomain.CurrentDomain.GetAssemblies(),
                a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

            if (loaded == null)
            {
                ed.WriteMessage("\n{0}: (not loaded)", name);
                return;
            }

            ed.WriteMessage("\n{0}: {1}", name, loaded.Location);
            ed.WriteMessage("\n         v{0}", loaded.GetName().Version);
        }
    }
}
