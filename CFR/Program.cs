using System;
using System.Collections.Generic;

namespace tarik
{
    public class Node                  // They didn't do this public but it doesnt work when the Node class is private NEED HELP
    {
        public string infoState;
        public int numActions;
        public double[] regretSum, strategy, strategySum;
        public Node(string infoState, int numActions)
        {
            this.infoState = infoState;
            this.numActions = numActions;
            this.regretSum = new double[numActions];
            this.strategy = new double[numActions];
            this.strategySum = new double[numActions];
        }
        // Get current information set mixed strategy through regret-matching
        public double[] getStrategy(double realizationWeight)
        {
            double normalizingSum = 0;                      // a double we have created to be able to "normalize" our strategy regrets, normalizing in this context means that we ensure the array entries sum up to 1 which means we can treat them like probabilities 
            for (int a = 0; a < numActions; a++)
            {
                strategy[a] = regretSum[a] > 0 ? regretSum[a] : 0;   // regret matching formula - max(0, regretSum[a])
                normalizingSum += strategy[a];             // updating normalizingSum so it is equal total "regretSum"s
            }
            for (int a = 0; a < numActions; a++)
            {
                if (normalizingSum > 0)                     // Checking if our normalizingSum is above 0,
                    strategy[a] /= normalizingSum;          // if so we divide strategy[a] with normalizingSum
                else
                    strategy[a] = 1.0 / (numActions);      // if normalizingSum isn't above 0 we update strategy[a]'s value to be equal to 1 / numActions 
                strategySum[a] += realizationWeight * strategy[a];              // after we check and update values with our normalizingSum to make sure we can use them as probabilities we add strategy[a] to our strategySum[a]
            }
            return strategy;                                // returning current strategy
        }
        public double[] getAverageStrategy()
        {
            /*
            Get average information set mixed strategy across all training iterations
            we do this because , the regrets may be temporarily skewed in such a way that an important strategy
            in the mix has a negative regret sum and would never be chosen
            we do the same thing we have done above with the exception of us not needing to worry about negative values
            */
            double[] avgStrategy = new double[numActions];              // creating new double array for average strategy
            double normalizingSum = 0;                                  // normalizingSum for making sure we can treat regrets as probabilities
            for (int a = 0; a < numActions; a++)
            {
                normalizingSum += strategySum[a];
            }                     // repeating until we ego through every possible action
                                  // updating normalizingSum with strategySum[a]
            for (int a = 0; a < numActions; a++)                        // repeating until we ego through every possible action
                if (normalizingSum > 0)                                 // checking if it's the first time we're running this
                    avgStrategy[a] = strategySum[a] / normalizingSum;   // if it isn't the first time we're running the program we we set the avgStrategy[a] regret value as strategySum[a] / normalizingSum
                else
                    avgStrategy[a] = 1.0 / numActions;                  // if it is the first time we're running the program we set avg strategy as 1.0 /  numActions  
            return avgStrategy;
        }
        // Get information set string representation
        public string toString()
        {
            return string.Format("{0}:\t{1}", this.infoState, string.Join(" ", getAverageStrategy()));
        }
    }

    class CFRTrainer
    {
        public int numActions;
        public static Random random = new Random();
        public int print_freq;
        public static Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();
        // Train Khun poker
        public CFRTrainer(int numActions, int print_freq)
        {
            this.numActions = numActions;
            this.print_freq = print_freq;
        }
        public void train(int iterations)
        {
            int[] cards = new int[] { 1, 2, 3 };                // Giving all cards
            double util = 0;
            for (int i = 0; i < iterations; i++)
            {
                // Shuffle cards
                for (int j = 0; j < cards.Length; j++)
                {
                    int temp = cards[j];
                    int randomIndex = random.Next(j, cards.Length);
                    cards[j] = cards[randomIndex];
                    cards[randomIndex] = temp;
                }
                util += cfr(cards, "", 1, 1);
            }
            Console.WriteLine("Average game value: " + util / iterations);
        }

        public Dictionary<string, Node> getNodeMap()
        {
            return nodeMap;
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
                bool terminalPass = Equals(history[plays - 1], 'p');                   // Checking if the last action was pass
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
            // Get information set: player_id+player_card+history
            string infoState = cards[player] + history;

            // Get information set node or create it if nonexistant
            // check if infostate in nodemap
            Node node;
            if (!nodeMap.ContainsKey(infoState))
            {
                node = new Node(infoState, numActions);
                nodeMap.Add(infoState, node);
            }
            else
            {
                node = nodeMap[infoState];
            }
            // For each action, recursively call cfr with additional history and probability
            double[] strategy = node.getStrategy(player == 0 ? p0 : p1);
            double[] util = new double[numActions];
            double nodeUtil = 0;
            for (int a = 0; a < numActions; ++a)
            {
                char newAction = (a == 0 ? 'p' : 'b');
                string nextHistory = history + newAction;
                util[a] = player == 0
                ? -cfr(cards, nextHistory, p0 * strategy[a], p1)
                : -cfr(cards, nextHistory, p0, p1 * strategy[a]);
                nodeUtil += strategy[a] * util[a];
            }
            //For each action, compute and accumulate counterfactual regret
            for (int a = 0; a < numActions; a++)
            {
                double regret = util[a] - nodeUtil;
                node.regretSum[a] += (player == 0 ? p1 : p0) * regret;
                // System.Console.WriteLine("Regret sum for action " + a + ": " + node.regretSum[a]);
            }
            return nodeUtil;
        }
    }

    class VanillaCFRRunner
    {
        public static Random random = new Random();
        static void Main(string[] args)
        {
            int iterations = 1000000;
            int numActions = 2;
            int print_freq = 100;
            CFRTrainer trainer = new CFRTrainer(numActions, print_freq);
            trainer.train(iterations);
            // Get the strategy from trainer
            Dictionary<string, Node> nodeMap = trainer.getNodeMap();
            // Print the key-value pairs in the dictionary
            foreach (KeyValuePair<string, Node> entry in nodeMap)
            {
                Console.WriteLine(entry.Value.toString());
            }

            // Play agains random opponent
            int num_rounds = 100;
            int num_games = 10000;
            int wins = 0, losses = 0, ties = 0;
            for (int i = 0; i < num_games; i++)
            {
                int player_value = 0;
                int opponent_value = 0;
                for (int r = 0; r < num_rounds; ++r)
                {
                    int round_value = playAgainstRandom(nodeMap, 0);
                    if (round_value > 0)
                        player_value += round_value;
                    else
                        opponent_value += round_value;
                }
                if (player_value > opponent_value)
                    wins++;
                else if (player_value < opponent_value)
                    losses++;
                else
                    ties++;
            }
            Console.WriteLine("Wins: " + wins + " Losses: " + losses + " Ties: " + ties);
            Console.WriteLine("Win rate: " + (double)wins / (double)(wins + losses + ties));
        }

        // Play against random opponent
        private static int playAgainstRandom(Dictionary<string, Node> nodeMap, int player_order)
        {
            int[] cards = new int[] { 1, 2, 3 };
            string history = "";

            // Shuffle cards
            for (int j = 0; j < cards.Length; j++)
            {
                int temp = cards[j];
                int randomIndex = random.Next(j, cards.Length);
                cards[j] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
            // Players receive cards
            int player = player_order;
            int opponent = 1 - player;
            int playerCard = cards[player];
            int opponentCard = cards[opponent];

            // Play until game ends
            while (true)
            {
                if (history.Length > 1)
                {
                    bool terminalPass = Equals(history.Substring(history.Length - 1), "p");
                    bool doubleBet = history.Substring(history.Length - 2).Equals("bb");
                    if (terminalPass)
                        if (history.Equals("pp"))
                            return playerCard > opponentCard ? 1 : -1;
                        else
                            return 1;
                    else if (doubleBet)
                        return playerCard > opponentCard ? 2 : -2;
                }
                // if players turn, choose action
                int a = 0;
                if (history.Length % 2 == player_order)
                {
                    string infoState = cards[player] + history;
                    double[] strategy = nodeMap[infoState].getAverageStrategy();
                    a = random.NextDouble() < strategy[0] ? 0 : 1;
                }
                else
                {
                    // if opponents turn, choose random action between 0 and 1
                    a = random.NextDouble() < 0.5 ? 0 : 1;
                }
                history += (a == 0 ? "p" : "b");
            } // end while
        } // end playAgainstRandom
    } // end class
} // end namespace
