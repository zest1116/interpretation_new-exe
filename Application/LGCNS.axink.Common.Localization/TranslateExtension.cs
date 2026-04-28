using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace LGCNS.axink.Common.Localization
{
    [MarkupExtensionReturnType(typeof(object))]
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TranslateExtension(string key) => Key = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // DynamicResourceExtension을 반환 → 런타임 교체 시 자동 갱신
            var dynamic = new DynamicResourceExtension(Key);
            return dynamic.ProvideValue(serviceProvider);
        }
    }
}
