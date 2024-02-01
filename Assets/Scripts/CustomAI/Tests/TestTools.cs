using System;
using UnityEngine;

namespace TestTools
{
    public class WaitUntilForSeconds : CustomYieldInstruction
    {
        float timer;
        Func<bool> myChecker;

        public WaitUntilForSeconds(Func<bool> myChecker, float timeout)
        {
            this.myChecker = myChecker;
            this.timer = timeout;
        }

        public override bool keepWaiting
        {
            get
            {
                bool checkThisTurn = myChecker();
                if (checkThisTurn || timer <= 0)
                {
                    return false;
                }

                timer -= Time.deltaTime;
                return true;
            }
        }
    }
}
