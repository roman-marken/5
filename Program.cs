using System.Collections.Concurrent;
using System.Text;

namespace EventsCriticalSectionsHomework;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Directory.CreateDirectory("output");

        while (true)
        {
            Console.Clear();
            Console.WriteLine("Домашнє завдання: Події. Критичні секції");
            Console.WriteLine("1 - Завдання 1: пари чисел, суми та добутки");
            Console.WriteLine("2 - Завдання 2: кінцева зупинка, один номер автобуса");
            Console.WriteLine("3 - Завдання 3: автобуси різних номерів");
            Console.WriteLine("4 - Завдання 4: не кінцева зупинка");
            Console.WriteLine("0 - Вихід");
            Console.Write("Ваш вибір: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    Task1NumberPairs.Run();
                    break;
                case "2":
                    BusSimulation.RunSingleRoute();
                    break;
                case "3":
                    BusSimulation.RunMultipleRoutes();
                    break;
                case "4":
                    BusSimulation.RunIntermediateStop();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Невідомий пункт меню.");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Натисніть Enter, щоб повернутися до меню...");
            Console.ReadLine();
        }
    }
}

internal static class Task1NumberPairs
{
    private const int PairCount = 20;
    private static readonly ManualResetEventSlim GenerationFinished = new(false);
    private static readonly object ConsoleLock = new();
    private static readonly Random Random = new();

    public static void Run()
    {
        GenerationFinished.Reset();

        var pairsFile = Path.Combine("output", "pairs.txt");
        var sumsFile = Path.Combine("output", "sums.txt");
        var productsFile = Path.Combine("output", "products.txt");

        var generator = new Thread(() => GeneratePairs(pairsFile));
        var sumCalculator = new Thread(() => CalculateSums(pairsFile, sumsFile));
        var productCalculator = new Thread(() => CalculateProducts(pairsFile, productsFile));

        generator.Start();
        sumCalculator.Start();
        productCalculator.Start();

        generator.Join();
        sumCalculator.Join();
        productCalculator.Join();

        WriteSafe("Завдання 1 завершено.");
        WriteSafe($"Файл пар: {Path.GetFullPath(pairsFile)}");
        WriteSafe($"Файл сум: {Path.GetFullPath(sumsFile)}");
        WriteSafe($"Файл добутків: {Path.GetFullPath(productsFile)}");
    }

    private static void GeneratePairs(string filePath)
    {
        var lines = new List<string>();

        for (var i = 0; i < PairCount; i++)
        {
            var first = Random.Next(1, 100);
            var second = Random.Next(1, 100);
            lines.Add($"{first} {second}");
            Thread.Sleep(50);
        }

        File.WriteAllLines(filePath, lines);
        WriteSafe("Потік 1: пари чисел згенеровано.");
        GenerationFinished.Set();
    }

    private static void CalculateSums(string inputFile, string outputFile)
    {
        GenerationFinished.Wait();

        var result = File.ReadAllLines(inputFile)
            .Select(ParsePair)
            .Select(pair => $"{pair.First} + {pair.Second} = {pair.First + pair.Second}");

        File.WriteAllLines(outputFile, result);
        WriteSafe("Потік 2: суми записано у файл.");
    }

    private static void CalculateProducts(string inputFile, string outputFile)
    {
        GenerationFinished.Wait();

        var result = File.ReadAllLines(inputFile)
            .Select(ParsePair)
            .Select(pair => $"{pair.First} * {pair.Second} = {pair.First * pair.Second}");

        File.WriteAllLines(outputFile, result);
        WriteSafe("Потік 3: добутки записано у файл.");
    }

    private static NumberPair ParsePair(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new NumberPair(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private static void WriteSafe(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }

    private sealed record NumberPair(int First, int Second);
}

internal static class BusSimulation
{
    private static readonly Random Random = new();

    public static void RunSingleRoute()
    {
        var stop = new BusStop("Кінцева зупинка");
        var simulation = new BusDaySimulation(stop, new[] { 175 }, isTerminalStop: true);

        simulation.PassengersArrived += (_, e) =>
            Console.WriteLine($"Прийшло {e.Count} пасажирів на автобус №{e.RouteNumber}. На зупинці: {stop.GetWaitingCount(e.RouteNumber)}");

        simulation.BusArrived += (_, e) =>
            Console.WriteLine($"Автобус №{e.Bus.RouteNumber} прибув. Сіло: {e.Boarded}. Залишилось на зупинці: {stop.GetWaitingCount(e.Bus.RouteNumber)}");

        simulation.DayFinished += (_, _) =>
            Console.WriteLine("Імітацію дня завершено.");

        simulation.Run();
    }

    public static void RunMultipleRoutes()
    {
        var stop = new BusStop("Кінцева зупинка");
        var simulation = new BusDaySimulation(stop, new[] { 175, 18, 44 }, isTerminalStop: true);

        simulation.PassengersArrived += (_, e) =>
            Console.WriteLine($"Прийшло {e.Count} пасажирів. Їм потрібен автобус №{e.RouteNumber}.");

        simulation.BusArrived += (_, e) =>
        {
            Console.WriteLine($"Прибув автобус №{e.Bus.RouteNumber}. Сіло: {e.Boarded}. Пасажирів у салоні: {e.Bus.PassengersInside}/{e.Bus.Capacity}.");
            Console.WriteLine(stop.CreateReport());
        };

        simulation.DayFinished += (_, _) =>
            Console.WriteLine("Імітацію з різними номерами автобусів завершено.");

        simulation.Run();
    }

    public static void RunIntermediateStop()
    {
        var stop = new BusStop("Проміжна зупинка");
        var simulation = new BusDaySimulation(stop, new[] { 175, 18, 44 }, isTerminalStop: false);

        simulation.PassengersArrived += (_, e) =>
            Console.WriteLine($"На проміжну зупинку прийшло {e.Count} пасажирів для автобуса №{e.RouteNumber}.");

        simulation.BusArrived += (_, e) =>
        {
            Console.WriteLine($"Автобус №{e.Bus.RouteNumber} приїхав з {e.InitialPassengers} пасажирами.");
            Console.WriteLine($"Вільних місць було: {e.FreeSeatsBeforeBoarding}. Сіло: {e.Boarded}. У салоні після посадки: {e.Bus.PassengersInside}/{e.Bus.Capacity}.");
            Console.WriteLine(stop.CreateReport());
        };

        simulation.DayFinished += (_, _) =>
            Console.WriteLine("Імітацію не кінцевої зупинки завершено.");

        simulation.Run();
    }

    private sealed class BusDaySimulation
    {
        private const int BusCount = 10;
        private const int BusCapacity = 30;
        private readonly BusStop _stop;
        private readonly int[] _routeNumbers;
        private readonly bool _isTerminalStop;
        private readonly ManualResetEventSlim _dayStarted = new(false);
        private readonly ConcurrentBag<Thread> _threads = new();

        public BusDaySimulation(BusStop stop, int[] routeNumbers, bool isTerminalStop)
        {
            _stop = stop;
            _routeNumbers = routeNumbers;
            _isTerminalStop = isTerminalStop;
        }

        public event EventHandler<PassengersArrivedEventArgs>? PassengersArrived;

        public event EventHandler<BusArrivedEventArgs>? BusArrived;

        public event EventHandler? DayFinished;

        public void Run()
        {
            var passengerThread = new Thread(GeneratePassengers);
            var busThread = new Thread(GenerateBuses);

            _threads.Add(passengerThread);
            _threads.Add(busThread);

            passengerThread.Start();
            busThread.Start();
            _dayStarted.Set();

            foreach (var thread in _threads)
            {
                thread.Join();
            }

            DayFinished?.Invoke(this, EventArgs.Empty);
        }

        private void GeneratePassengers()
        {
            _dayStarted.Wait();

            for (var i = 0; i < BusCount; i++)
            {
                Thread.Sleep(NextRandom(120, 350));

                var route = GetRandomRoute();
                var count = NextRandom(5, 26);
                _stop.AddPassengers(route, count);

                PassengersArrived?.Invoke(this, new PassengersArrivedEventArgs(route, count));
            }
        }

        private void GenerateBuses()
        {
            _dayStarted.Wait();

            for (var i = 0; i < BusCount; i++)
            {
                Thread.Sleep(NextRandom(300, 650));

                var route = GetRandomRoute();
                var initialPassengers = _isTerminalStop ? 0 : NextRandom(0, BusCapacity);
                var bus = new Bus(route, BusCapacity, initialPassengers);
                var freeSeats = bus.FreeSeats;
                var boarded = _stop.BoardPassengers(bus.RouteNumber, bus.FreeSeats);

                bus.PassengersInside += boarded;
                BusArrived?.Invoke(this, new BusArrivedEventArgs(bus, initialPassengers, freeSeats, boarded));
            }
        }

        private int GetRandomRoute()
        {
            return _routeNumbers[NextRandom(_routeNumbers.Length)];
        }
    }

    private static int NextRandom(int maxValue)
    {
        lock (Random)
        {
            return Random.Next(maxValue);
        }
    }

    private static int NextRandom(int minValue, int maxValue)
    {
        lock (Random)
        {
            return Random.Next(minValue, maxValue);
        }
    }

    private sealed class BusStop
    {
        private readonly object _criticalSection = new();
        private readonly Dictionary<int, int> _waitingPassengers = new();

        public BusStop(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void AddPassengers(int routeNumber, int count)
        {
            lock (_criticalSection)
            {
                _waitingPassengers.TryAdd(routeNumber, 0);
                _waitingPassengers[routeNumber] += count;
            }
        }

        public int BoardPassengers(int routeNumber, int freeSeats)
        {
            lock (_criticalSection)
            {
                _waitingPassengers.TryAdd(routeNumber, 0);

                var boarded = Math.Min(_waitingPassengers[routeNumber], freeSeats);
                _waitingPassengers[routeNumber] -= boarded;
                return boarded;
            }
        }

        public int GetWaitingCount(int routeNumber)
        {
            lock (_criticalSection)
            {
                return _waitingPassengers.GetValueOrDefault(routeNumber);
            }
        }

        public string CreateReport()
        {
            lock (_criticalSection)
            {
                if (_waitingPassengers.Count == 0)
                {
                    return $"{Name}: пасажирів немає.";
                }

                var lines = _waitingPassengers
                    .OrderBy(item => item.Key)
                    .Select(item => $"№{item.Key}: очікує {item.Value}");

                return $"{Name}: " + string.Join("; ", lines);
            }
        }
    }

    private sealed class Bus
    {
        public Bus(int routeNumber, int capacity, int passengersInside)
        {
            RouteNumber = routeNumber;
            Capacity = capacity;
            PassengersInside = passengersInside;
        }

        public int RouteNumber { get; }

        public int Capacity { get; }

        public int PassengersInside { get; set; }

        public int FreeSeats => Capacity - PassengersInside;
    }

    private sealed record PassengersArrivedEventArgs(int RouteNumber, int Count);

    private sealed record BusArrivedEventArgs(Bus Bus, int InitialPassengers, int FreeSeatsBeforeBoarding, int Boarded);
}
