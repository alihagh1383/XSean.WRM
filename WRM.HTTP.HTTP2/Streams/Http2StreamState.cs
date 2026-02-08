namespace WRM.HTTP.HTTP2.Streams;

public enum Http2StreamState
{
    Idle,             // وضعیت اولیه؛ هنوز هیچ فریمی ارسال یا دریافت نشده
    ReservedLocal,    // وقتی سرور یک PUSH_PROMISE ارسال می‌کنه
    ReservedRemote,   // وقتی کلاینت یک PUSH_PROMISE دریافت می‌کنه
    Open,             // هر دو طرف می‌تونن فریم ارسال کنن (وضعیت فعال)
    HalfClosedLocal,  // ما ارسال دیتا رو تمام کردیم (END_STREAM فرستادیم) اما هنوز دیتا می‌گیریم
    HalfClosedRemote, // طرف مقابل ارسال رو تمام کرده اما ما هنوز می‌تونیم بفرستیم
    Closed            // استریم کاملاً بسته شده و شناسه‌اش دیگه معتبر نیست
}