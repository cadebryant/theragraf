using Microsoft.Azure.Functions.Worker;
using OpenAI.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Theragraf.Core.Models;

namespace Theragraf.Functions.Activities
{
    public class IngestionActivity
    {
        [Function(nameof(IngestionActivity))]
        public async Task<ObservationResult> Run([ActivityTrigger] TranscriptInput input)
        {
            throw new NotImplementedException();
        }
    }
}
