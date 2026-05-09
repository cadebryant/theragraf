using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Theragraf.Core.Models;

namespace Theragraf.Functions.Activities
{
    public class SoapActivity
    {
        [Function(nameof(SoapActivity))]
        public async Task<SoapNote> Run([ActivityTrigger] ObservationResult input)
        {
            throw new NotImplementedException();
        }
    }
}
