using System.Collections.Generic;

namespace StackQueueDemo
{
	class Program
	{
		static void Main(string[] args)
		{
			Queue<string> queue = new Queue<string>();

			queue.Enqueue("Rafo");
			queue.Enqueue("Raf");
			queue.Enqueue("Ra");
			queue.Enqueue("R");
			queue.Enqueue("Raff");
			queue.Enqueue("RR");
			queue.Enqueue("RR");
			queue.Enqueue("RR");

			queue.Dequeue();
			queue.Dequeue();

			queue.Enqueue("AA");
		}
	}
}
