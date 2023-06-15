namespace VeninethTrainer
{
    public struct SpeedPoint
    {
        public double Time;
        public double Speed;

        public override string ToString()
        {
            return $"{Time},{Speed}";
        }
    }
}