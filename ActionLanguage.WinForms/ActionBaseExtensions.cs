using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActionLanguage
{
    public static class ActionBaseExtensions
    {
        public static bool ConfigurationMenu(this ActionBase action, System.Windows.Forms.Form parent, ActionCoreController cp, List<BaseUtils.TypeHelpers.PropertyNameInfo> eventvars)
        {
            return action.Configure(cp, eventvars, new ActionConfigFuncsWinForms(parent));
        }
    }
}
