/***************************************************
 * Mã sinh viên: 202418909
 * Họ tên: Trần Quốc Hùng
 ***************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace CampusSafetyHub
{
    // =========================================================
    // 1. ADAPTER PATTERN
    // =========================================================

    /// <summary>
    /// Giao diện chuẩn cho các cảm biến trong hệ thống giám sát
    /// </summary>
    public interface ISensor
    {
        double ReadValue();
        string GetUnit();
    }

    /// <summary>
    /// Lớp cảm biến cũ sẵn có (Giả định không được sửa đổi)
    /// </summary>
    public sealed class LegacySmokeDetector
    {
        private int smokeLevel;

        public LegacySmokeDetector(int smokeLevel)
        {
            this.smokeLevel = Math.Clamp(smokeLevel, 0, 1000);
        }

        public int GetSmokeLevel() => smokeLevel; // Thang đo 0..1000
        public string GetDescription() => "Legacy smoke detector";
        public void SetSmokeLevel(int value)
        {
            smokeLevel = Math.Clamp(value, 0, 1000);
        }
    }

    /// <summary>
    /// Adapter chuyển đổi giao diện và thang đo từ 0..1000 sang 0..100%
    /// </summary>
    public sealed class SmokeSensorAdapter : ISensor
    {
        private readonly LegacySmokeDetector legacy;

        public SmokeSensorAdapter(LegacySmokeDetector legacy)
        {
            this.legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public double ReadValue()
        {
            // Chuyển đổi từ thang đo 0..1000 sang phần trăm 0..100%
            return legacy.GetSmokeLevel() / 10.0;
        }

        public string GetUnit() => "% smoke";
    }

    // =========================================================
    // 2. OBSERVER PATTERN
    // =========================================================

    /// <summary>
    /// Đối tượng dữ liệu bất biến mô tả cảnh báo
    /// </summary>
    public sealed record AlertEvent(
        string SensorName,
        double Value,
        string Unit,
        string Severity,
        DateTime OccurredAt);

    /// <summary>
    /// Hợp đồng chung cho các bên nhận thông báo cảnh báo
    /// </summary>
    public interface IAlertObserver
    {
        void Update(AlertEvent alertEvent);
    }

    /// <summary>
    /// Lớp giám sát (Subject) quản lý Observers và kiểm tra ngưỡng cảm biến
    /// </summary>
    public sealed class SensorMonitor
    {
        private readonly List<IAlertObserver> observers = new();

        public void Attach(IAlertObserver observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
        }

        public void Detach(IAlertObserver observer)
        {
            observers.Remove(observer);
        }

        public void CheckSensor(string sensorName, ISensor sensor)
        {
            ArgumentNullException.ThrowIfNull(sensor);
            double value = sensor.ReadValue();

            string? severity = value switch
            {
                >= 70 => "CRITICAL",
                >= 50 => "WARNING",
                _ => null
            };

            if (severity is null)
            {
                return; // Dưới 50%: Trạng thái bình thường, không phát cảnh báo
            }

            var alertEvent = new AlertEvent(
                sensorName,
                value,
                sensor.GetUnit(),
                severity,
                DateTime.Now);

            Notify(alertEvent);
        }

        private void Notify(AlertEvent alertEvent)
        {
            // Duyệt trên bản sao ToArray() để an toàn nếu có observer bị gỡ trong lúc thông báo
            foreach (IAlertObserver observer in observers.ToArray())
            {
                try
                {
                    observer.Update(alertEvent);
                }
                catch (Exception ex)
                {
                    // Cô lập ngoại lệ: 1 observer lỗi không làm ảnh hưởng observer khác
                    Console.WriteLine($" [SENSOR MONITOR ERROR] Exception caught in {observer.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    // --- CONCRETE OBSERVERS ---

    public sealed class DashboardObserver : IAlertObserver
    {
        public void Update(AlertEvent alertEvent)
        {
            Console.WriteLine($" [DASHBOARD] {alertEvent.SensorName}: {alertEvent.Value:F1}{alertEvent.Unit} [{alertEvent.Severity}]");
        }
    }

    public sealed class EmailAlertObserver : IAlertObserver
    {
        public void Update(AlertEvent alertEvent)
        {
            Console.WriteLine($" [EMAIL] Sent alert email: {alertEvent.SensorName} measured {alertEvent.Value:F1}{alertEvent.Unit} [{alertEvent.Severity}]");
        }
    }

    public sealed class AuditLogObserver : IAlertObserver
    {
        public void Update(AlertEvent alertEvent)
        {
            Console.WriteLine($" [AUDIT LOG] Recorded event: {alertEvent.SensorName} - {alertEvent.Value:F1}{alertEvent.Unit} [{alertEvent.Severity}]");
        }
    }

    // =========================================================
    // 3. MEDIATOR PATTERN & EMERGENCY BRIDGE
    // =========================================================

    /// <summary>
    /// Các thiết bị ứng phó sự cố khẩn cấp (Colleagues)
    /// </summary>
    public sealed class Alarm
    {
        public void SoundAlarm()
        {
            Console.WriteLine("   --> [ALARM] Siren sounding at maximum volume!");
        }
    }

    public sealed class DoorController
    {
        public void UnlockEmergencyDoors()
        {
            Console.WriteLine("   --> [DOOR CONTROLLER] All emergency exit doors UNLOCKED!");
        }
    }

    public sealed class VentilationController
    {
        public void StartSmokeExtraction()
        {
            Console.WriteLine("   --> [VENTILATION] Smoke extraction fans STARTED at 100% capacity!");
        }
    }

    /// <summary>
    /// Hợp đồng điều phối sự cố
    /// </summary>
    public interface IEmergencyMediator
    {
        void Notify(string eventType, AlertEvent alertEvent);
    }

    /// <summary>
    /// Mediator tập trung điều phối các thiết bị ứng phó
    /// </summary>
    public sealed class EmergencyMediator : IEmergencyMediator
    {
        private readonly Alarm alarm;
        private readonly DoorController doorController;
        private readonly VentilationController ventilationController;

        public EmergencyMediator(Alarm alarm, DoorController doorController, VentilationController ventilationController)
        {
            this.alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
            this.doorController = doorController ?? throw new ArgumentNullException(nameof(doorController));
            this.ventilationController = ventilationController ?? throw new ArgumentNullException(nameof(ventilationController));
        }

        public void Notify(string eventType, AlertEvent alertEvent)
        {
            if (eventType == "FIRE_DETECTED")
            {
                Console.WriteLine($" [MEDIATOR] Handling event: FIRE_DETECTED triggered by {alertEvent.SensorName}");
                alarm.SoundAlarm();
                doorController.UnlockEmergencyDoors();
                ventilationController.StartSmokeExtraction();
            }
        }
    }

    /// <summary>
    /// Observer đóng vai trò Bridge chuyển tiếp cảnh báo CRITICAL tới EmergencyMediator
    /// </summary>
    public sealed class EmergencyBridgeObserver : IAlertObserver
    {
        private readonly IEmergencyMediator mediator;

        public EmergencyBridgeObserver(IEmergencyMediator mediator)
        {
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        public void Update(AlertEvent alertEvent)
        {
            if (alertEvent.Severity == "CRITICAL")
            {
                Console.WriteLine(" [BRIDGE] CRITICAL alert detected! Notifying EmergencyMediator...");
                mediator.Notify("FIRE_DETECTED", alertEvent);
            }
        }
    }

    /// <summary>
    /// Observer thử nghiệm cố tình gây ngoại lệ để kiểm thử cô lập lỗi
    /// </summary>
    public sealed class FaultyObserver : IAlertObserver
    {
        public void Update(AlertEvent alertEvent)
        {
            throw new InvalidOperationException("FaultyObserver crashed intentionally!");
        }
    }

    // =========================================================
    // 4. MAIN PROGRAM & TEST CASES
    // =========================================================

    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==========================================================================");
            Console.WriteLine("    CAMPUS SAFETY HUB - BÀI THỰC HÀNH MẪU THIẾT KẾ (LAB W11)             ");
            Console.WriteLine("==========================================================================\n");

            // 1. Khởi tạo Cảm biến cũ & Adapter
            var legacyDetector = new LegacySmokeDetector(735); // 735/1000 = 73.5%
            ISensor adapter = new SmokeSensorAdapter(legacyDetector);

            Console.WriteLine(">>> 1. TEST ADAPTER PATTERN <<<");
            Console.WriteLine($"Giá trị đọc từ cảm biến cũ (735/1000): {adapter.ReadValue():F1}{adapter.GetUnit()}");
            Console.WriteLine("[OK] SmokeSensorAdapter chuyển đổi thành công thang đo 0..1000 sang 0..100%.\n");

            // 2. Khởi tạo Mediator & Colleagues
            var alarm = new Alarm();
            var doorController = new DoorController();
            var ventController = new VentilationController();
            IEmergencyMediator mediator = new EmergencyMediator(alarm, doorController, ventController);

            // 3. Khởi tạo Observers
            var dashboard = new DashboardObserver();
            var emailAlert = new EmailAlertObserver();
            var auditLog = new AuditLogObserver();
            var emergencyBridge = new EmergencyBridgeObserver(mediator);

            // 4. Đăng ký Observers vào Subject (SensorMonitor)
            var monitor = new SensorMonitor();
            monitor.Attach(dashboard);
            monitor.Attach(emailAlert);
            monitor.Attach(auditLog);
            monitor.Attach(emergencyBridge);

            // 5. Test Mức BÌNH THƯỜNG (<50%)
            Console.WriteLine(">>> 2. TEST MỨC BÌNH THƯỜNG (<50%) <<<");
            legacyDetector.SetSmokeLevel(350); // 35.0%
            Console.WriteLine("Cảm biến đo được: 35.0% smoke -> Trạng thái: Bình thường (Không có cảnh báo phát ra).");
            monitor.CheckSensor("Smoke Sensor #1", adapter);
            Console.WriteLine();

            // 6. Test Mức WARNING (50% - 69.9%)
            Console.WriteLine(">>> 3. TEST MỨC WARNING (50% - 69.9%) <<<");
            legacyDetector.SetSmokeLevel(550); // 55.0%
            Console.WriteLine("Cảm biến đo được: 55.0% smoke -> Phát cảnh báo WARNING:");
            monitor.CheckSensor("Smoke Sensor #1", adapter);
            Console.WriteLine();

            // 7. Test Mức CRITICAL (>=70%) & Kích hoạt Mediator
            Console.WriteLine(">>> 4. TEST MỨC CRITICAL (>=70%) & KÍCH HOẠT MEDIATOR <<<");
            legacyDetector.SetSmokeLevel(735); // 73.5%
            Console.WriteLine("Cảm biến đo được: 73.5% smoke -> Phát cảnh báo CRITICAL & Kích hoạt Mediator:");
            monitor.CheckSensor("Smoke Sensor #1", adapter);
            Console.WriteLine();

            // 8. Test Chống đăng ký trùng lặp & Hủy đăng ký
            Console.WriteLine(">>> 5. TEST CHỐNG ĐĂNG KÝ TRÙNG LẶP & HUỶ ĐĂNG KÝ (DETACH) <<<");
            monitor.Attach(dashboard); // Đăng ký lại dashboard -> Không bị nhân đôi
            Console.WriteLine("Thử đăng ký lại DashboardObserver -> Đã bị chặn trùng lặp.");
            monitor.Detach(emailAlert);
            Console.WriteLine("Huỷ đăng ký EmailAlertObserver thành công.\nPhát lại cảnh báo WARNING:");
            legacyDetector.SetSmokeLevel(600);
            monitor.CheckSensor("Smoke Sensor #1", adapter);
            Console.WriteLine();

            // 9. Test Cô lập Ngoại lệ (Exception Isolation)
            Console.WriteLine(">>> 6. TEST CÔ LẬP NGOẠI LỆ (EXCEPTION ISOLATION) <<<");
            var faultyObserver = new FaultyObserver();
            monitor.Attach(faultyObserver);
            Console.WriteLine("Thêm FaultyObserver (gây lỗi khi Update)... Kiểm tra tính ổn định hệ thống:");
            legacyDetector.SetSmokeLevel(800);
            monitor.CheckSensor("Smoke Sensor #1", adapter);
            Console.WriteLine("\n[OK] Các Observers khác vẫn nhận thông báo bình thường dù có 1 Observer bị lỗi.");
        }
    }
}