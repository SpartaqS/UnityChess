using System;
using UnityEngine;

namespace TestTools
{
    public class WaitUntilForSeconds : CustomYieldInstruction
    {
        float timeToStopAt;
        Func<bool> myChecker;
        Action onTimeout;

        public WaitUntilForSeconds(Func<bool> myChecker, float timeout, Action onTimeout)
        {
            this.myChecker = myChecker;
            this.timeToStopAt = Time.unscaledTime + timeout;
            this.onTimeout = onTimeout;
        }

        public WaitUntilForSeconds(Func<bool> myChecker, float timeout) : this(myChecker, timeout, null) { }
        public override bool keepWaiting
        {
            get
            {
                bool checkThisTurn = myChecker();
                float currentTime = Time.unscaledTime;
                if (checkThisTurn || currentTime >= timeToStopAt) // condition fulfilled or timeout time has been exceeded
                {
                    if (currentTime >= timeToStopAt && onTimeout != null)
					{
                        onTimeout();
					}
                    return false;
                }

                return true;
            }
        }
    }
}
