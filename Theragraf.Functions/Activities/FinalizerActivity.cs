using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Theragraf.Core.Models;

namespace Theragraf.Functions.Activities
{
    public class FinalizerActivity
    {
        [Function(nameof(FinalizerActivity))]
        public async Task<FinalizeResult> Run([ActivityTrigger] ComplianceResult input)
        {
            throw new NotImplementedException();
        }
    }
}
