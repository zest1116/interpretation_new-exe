using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting.Contracts
{
    public sealed record SttEnvelope
        (
            string Type,
            object? Payload
        );
}
