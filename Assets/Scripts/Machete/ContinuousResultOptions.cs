using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{

    public class ContinuousResultOptions
    {
        public int latencyFrameCount;

        public bool individualBoundary;

        public bool abandon;

        public ContinuousResultOptions()
        {
            latencyFrameCount = 0;
            individualBoundary = false;
            abandon = false;
        }
    }

    public class ContinuousResult
    {
        public enum ResultStateT
        {
            /**
             * In this state, we are waiting for the score to
             * fall below the rejection threshold.
             */
            WAIT_FOR_START = 0,

            /**
             * Here, we've fallen below the rejection threshold, 
             * but we've not yet seen the best score. While the
             * score is still dropping, we continue to wait.
             */
            LOOKING_FOR_MINIMUM = 1,

            /**
             * We believe we've found the minimum score and we
             * are going to report it this frame. Hang out here
             * for just one frame so the application can see that
             * we've 'triggered' a recognition.
             */
            TRIGGER = 2,

            /**
             * After reporting that we've recognized a gesture, 
             * wait for the score to climb back out of the rejection
             * threshold area. This is also needed if the application
             * doesn't reset the system whenever a gesture is
             * recognized.
             */
            WAIT_FOR_END = 3,
        };

        /**
         * Internal Recognition Result state
         */
        ResultStateT state;

        public double score;

        public ContinuousResultOptions options;

        public int gid;

        public int boundary;

        /**
         * Minimum warping path score observed
         */
        public double minimum;

        /**
         * The starting frame of the minimum warping path.
         */
        public int startFrameNo;

        /**
         * The ending frame of the minimum warping path.
         */
        public int endFrameNo;

        /**
         * Rejection threshold
         */
        public double rejection_threshold;

        public Sample sample;

        /**
         * Default constructor
         */
        public ContinuousResult() { }

        /**
         * Constructor
         */
        public ContinuousResult(
            ContinuousResultOptions options,
            int gid,
            Sample _sample
            )
        {
            this.options = options;
            this.gid = gid;

            this.Reset();
            this.boundary = -1;

            this.sample = _sample;
        }

        public void SetWaitForStart()
        {
            // Initial state
            state = ResultStateT.WAIT_FOR_START;

            // No minimum found yet
            minimum = double.PositiveInfinity;
            score = minimum;

            // Gesture Boundaries
            startFrameNo = -1;
            endFrameNo = -1;
        }

        public void Reset()
        {
            SetWaitForStart();
        }

        /**
         * Based on internal criteria, was a gesture
         * officially recognized?
         */
        public bool Triggered()
        {
            return (state == ResultStateT.TRIGGER);
        }

        /**
         * Update internal FSM based on current score.
         */
        public void Update(
            double score,
            double threshold,
            int start_frame_no,
            int end_frame_no,
            int current_frame_no)
        {
            // Set up current frame if necessary
            if (current_frame_no == -2)
                current_frame_no = end_frame_no;

            // Wait for the score to pass below the
            // rejection threshold
            if (state == ResultStateT.WAIT_FOR_START)
            {
                // If waiting, make sure the minimum matches
                // the actual score.
                minimum = score;

                if (score < threshold)
                {
                    state = ResultStateT.LOOKING_FOR_MINIMUM;
                }
            }

            // Although the gesture is in the accept zone, we
            // may have not reached the best score yet.
            if (state == ResultStateT.LOOKING_FOR_MINIMUM)
            {
                // save data on new minimum
                if (score <= minimum)
                {
                    minimum = score;
                    this.score = minimum;
                    this.startFrameNo = start_frame_no;
                    this.endFrameNo = end_frame_no;
                }

                //
                // Timeout
                //
                int frame_cnt = current_frame_no - this.endFrameNo;
                bool timeout = (frame_cnt >= options.latencyFrameCount);
                if (timeout == true)
                {
                    state = ResultStateT.TRIGGER;
                    return;
                }
            }

            // Trigger operation is complete, so advance.
            if (state == ResultStateT.TRIGGER)
            {
                state = ResultStateT.WAIT_FOR_END;
            }

            // Wait until we leave the accept zone
            // to avoid triggering again.
            if (state == ResultStateT.WAIT_FOR_END)
            {
                bool advance = true;

                //advance &= start_frame_no > boundary;
                advance &= score > rejection_threshold;

                if (advance)
                {
                    // this.Reset();
                    state = ResultStateT.WAIT_FOR_START;
                }
            }
        }

        /**
         * Called when a gesture is recognized.
         */
        public void SetWaitForEnd(ContinuousResult result)
        {
            boundary = result.endFrameNo;
            state = ResultStateT.WAIT_FOR_END;
        }

        /**
         * Call when a false positive has occurred. 
         */
        public void FalsePositive(ContinuousResult result)
        {
            // Reset internal state should handle everything.
            this.Reset();

            // Do not reset boundary, because the issue may 
            // just be poor segmentation. So allow new scores
            // to result is quick recognition. 
        }

        /**
         * Get name of state
         */
        public string StateStr()
        {
            switch (state)
            {
                case ResultStateT.LOOKING_FOR_MINIMUM:
                    return "looking for minimum";
                case ResultStateT.WAIT_FOR_START:
                    return "wait for start";
                case ResultStateT.TRIGGER:
                    return "trigger";
                case ResultStateT.WAIT_FOR_END:
                    return "wait for end";
            }

            return "impossible ResultStateT case";
        }

        public static ContinuousResult SelectResult(
            List<ContinuousResult> results,
            bool cancel_with_something_better
            )
        {
            List<ContinuousResult> triggered = new List<ContinuousResult>();
            List<ContinuousResult> remaining = new List<ContinuousResult>();

            // Get all triggered events
            for (int ii = 0; ii < results.Count; ii++)
            {
                ContinuousResult result = results[ii];

                if (!result.Triggered())
                    continue;

                triggered.Add(result);
            }

            // If none triggered notn to do
            if (triggered.Count == 0)
                return null;

            for (int ii = 0; ii < triggered.Count; ii++)
            {
                for (int jj = 0; jj < results.Count; jj++)
                {
                    ContinuousResult result = results[jj];

                    if (triggered[ii] == result)
                        continue;

                    if (triggered[ii].minimum > result.minimum)
                    {
                        if (cancel_with_something_better == true)
                        {
                            triggered[ii].SetWaitForEnd(result);
                            break;
                        }
                    }
                }

                if (triggered[ii].Triggered())
                    remaining.Add(triggered[ii]);
            }

            // Get the best survivor
            if (remaining.Count == 0)
                return null;

            return remaining[0];
        }
    }
}