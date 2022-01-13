using System;
using System.Linq;
using System.Collections.Generic;


namespace CFR
{       
    public class KuhnTrainer
    {
        // Kuhn poker definitions
        public static int PASS = 0, BET = 1, NUM_ACTIONS = 2;
        public static Random random = new Random();
        public static Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();
        // Information set node class definition
        public class Node                  // They didn't do this public but it doesnt work when the Node class is private NEED HELP
        {
            // Kuhn node definitions
            public string infoSet = "";
            public double[] regretSum = new double[NUM_ACTIONS],
                            strategy = new double[NUM_ACTIONS],
                            strategySum = new double[NUM_ACTIONS];
            
            
            // Get current information set mixed strategy through regret-matching
            public double[] getStrategy(double realizationWeight)
            {
                double normalizingSum = 0;                      // a double we have created to be able to "normalize" our strategy regrets, normalizing in this context means that we ensure the array entries sum up to 1 which means we can treat them like probabilities 
                for (int a = 0; a < NUM_ACTIONS; a++)
                {
                    strategy[a] = regretSum[a] > 0 ? regretSum[a] : 0;      // ASK
                    normalizingSum += regretSum[a];             // updating normalizingSum so it is equal total "regretSum"s
                }
                for (int a = 0; a < NUM_ACTIONS; a++)
                {
                    if (normalizingSum > 0)                     // Checking if our normalizingSum is above 0,
                        strategy[a] /= normalizingSum;          // if so we divide strategy[a] with normalizingSum
                    else
                        strategy[a] = 1.0 / (NUM_ACTIONS);      // if normalizingSum isn't above 0 we update strategy[a]'s value to be equal to 1 / NUM_ACTIONS 
                    strategySum[a] += strategy[a];              // after we check and update values with our normalizingSum to make sure we can use them as probabilities we add strategy[a] to our strategySum[a]
                }
                return strategy;                                // returning current strategy
            }
            // Get average information set mixed strategy across all training iterations
            // we do this because , the regrets may be temporarily skewed in such a way that an important strategy
            // in the mix has a negative regret sum and would never be chosen
            // we do the same thing we have done above with the exception of us not needing to worry about negative values
            public double[] getAverageStrategy()
            {
                double[] avgStrategy = new double[NUM_ACTIONS];             // creating new double array for average strategy
                double normalizingSum = 0;                                  // normalizingSum for making sure we can treat regrets as probabilities
                for (int a = 0; a < NUM_ACTIONS; a++)                       // repeating until we ego through every possible action
                    normalizingSum += strategySum[a];                       // updating normalizingSum with strategySum[a]
                for (int a = 0; a < NUM_ACTIONS; a++)                       // repeating until we ego through every possible action
                    if (normalizingSum > 0)                                 // checking if it's the first time we're running this
                        avgStrategy[a] = strategySum[a] / normalizingSum;   // if it isn't the first time we're running the program we we set the avgStrategy[a] regret value as strategySum[a] / normalizingSum
                    else
                        avgStrategy[a] = 1.0 / NUM_ACTIONS;                 // if it is the first time we're running the program we set avg strategy as 1.0 /  NUM_ACTIONS  
                return avgStrategy;
                
            }
            // Get information set string representation
            public string ToString()
            {
                return string.Format("{0}:{1}", infoSet, string.Join(" ", getAverageStrategy()));
            }
        }
        // Train Khun poker
        public void train(int iterations)
        {
            int[] cards = new int[] { 1, 2, 3 };                // Giving all cards
            double util = 0;
            for (int i = 0; i < iterations; i++)
            {
                // Shuffle cards
                for (int c1 = cards.Length - 1; c1 > 0; c1--)
                {
                    int c2 = random.Next(c1 + 1);
                    int tmp = cards[c1];
                    cards[c1] = cards[c2];
                    cards[c2] = tmp;
                }
                util += cfr(cards, "", 1, 1);
            }
            Console.WriteLine("Average game value: " + util / iterations);
            foreach (Node n in nodeMap.Values)
                Console.WriteLine(n);
        }
        // Counterfactual regret minimization iteration
        private double cfr(int[] cards, string history, double p0, double p1)
        {
            int plays = history.Length;
            int player = plays % 2;
            int opponent = 1 - player;

            // Return payoff for terminal states
            if (plays > 1)                                                            // We check if both players got to make an action         
            {
                bool terminalPass = Equals(history[plays - 1], "p");                  // Checking if the last action was pass
                bool doubleBet = history.Substring(plays - 2).Equals("bb");           // Checking if two back to back bets have been done
                bool isPlayerCardHigher = cards[player] > cards[opponent];            // Checking if players card is higher than the opponents
                if (terminalPass)                                                     // Check if iterminalPass is true 
                    if (history.Equals("pp"))                                         // Check if it was two passes back to back 
                        return isPlayerCardHigher ? 1 : -1;                           // (this results in the game ending and the player with the higher card winning 1 point)
                    else                                                              // If it wasn't two passes it means the last player passed after a bet which ends the game
                        return 1;
                else if (doubleBet)                                                   // Checking if it was a double bet instead of the last action being a pass
                    return isPlayerCardHigher ? 2 : -2;                               // if so we give the player with higher card 2 points
            }
            string infoSet = cards[player] + history;   

            // Get information set node or create it if nonexistant
            //Node node = nodeMap[infoSet];      // NEED HELP
            if (nodeMap.ContainsKey(infoSet)) ; Node node = nodeMap[infoSet];
            if (node == null)
            {
                node = new Node();
                node.infoSet= infoSet;
                nodeMap.Add(infoSet, node);
            }   
            //For each action, recursively call cfr with additional history and probability
            double[] strategy = node.getStrategy(player == 0 ? p0 : p1);
            double[] util = new double[NUM_ACTIONS];
            double nodeUtil = 0;
            for (int a = 0; a < NUM_ACTIONS; a++)
            {
                string nextHistory = history + (a == 0 ? "p" : "b");
                util[a] = player == 0
                ? -cfr(cards, nextHistory, p0 * strategy[a], p1)
                : -cfr(cards, nextHistory, p0, p1 * strategy[a]);
                nodeUtil += strategy[a] * util[a];
            }
            //For each action, compute and accumulate counterfactual regret
            for (int a = 0; a < NUM_ACTIONS; a++)
            {
                double regret = util[a] - nodeUtil;
                node.regretSum[a] += (player == 0 ? p1 : p0) * regret;
            }
            return nodeUtil;
        }
    }
    public class Program
    {   
        static void Main(string[] args)
        {
            int iterations = 10000000;
            KuhnTrainer newTrainer = new KuhnTrainer();
            newTrainer.train(iterations);
        }
    }
}
    