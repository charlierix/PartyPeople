using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    /// <summary>
    /// After a file is loaded, this gets called to do some post process analysis
    /// </summary>
    public class EdgeBackgroundWorker
    {
        #region record: WorkerRequest

        public record WorkerRequest
        {

        }

        #endregion
        #region record: WorkerResponse

        public record WorkerResponse
        {

        }

        #endregion

        public static WorkerResponse DoWork(WorkerRequest args, CancellationToken cancel)
        {
            return new WorkerResponse()
            {

            };
        }

    }
}
