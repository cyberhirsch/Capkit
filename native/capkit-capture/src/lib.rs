//! Spike: C ABI around scap so the .NET app can capture a screen region
//! through one native library on Windows and macOS.

use scap::capturer::{Area, Capturer, Options, Point, Size};
use scap::frame::{Frame, FrameType};
use std::panic::catch_unwind;

#[repr(C)]
pub struct CkFrame {
    pub data: *mut u8,
    pub len: usize,
    pub width: i32,
    pub height: i32,
    pub stride: i32,
}

pub const CK_OK: i32 = 0;
pub const CK_NOT_SUPPORTED: i32 = 1;
pub const CK_NO_PERMISSION: i32 = 2;
pub const CK_NO_FRAME: i32 = 3;
pub const CK_UNEXPECTED_FORMAT: i32 = 4;
pub const CK_PANIC: i32 = 5;
pub const CK_INVALID_ARGUMENT: i32 = 6;

#[no_mangle]
pub extern "C" fn ck_is_supported() -> bool {
    catch_unwind(scap::is_supported).unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn ck_has_permission() -> bool {
    catch_unwind(scap::has_permission).unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn ck_request_permission() -> bool {
    catch_unwind(scap::request_permission).unwrap_or(false)
}

/// Captures a region of the main display. Coordinates are logical (unscaled) units
/// relative to the display's top-left corner; width or height of 0 captures the whole display.
#[no_mangle]
pub extern "C" fn ck_capture_main_display(x: f64, y: f64, width: f64, height: f64, show_cursor: bool, out: *mut CkFrame) -> i32 {
    if out.is_null() {
        return CK_INVALID_ARGUMENT;
    }

    let result = catch_unwind(|| capture(x, y, width, height, show_cursor));

    match result {
        Ok(Ok(frame)) => {
            unsafe { *out = frame };
            CK_OK
        }
        Ok(Err(code)) => code,
        Err(_) => CK_PANIC,
    }
}

#[no_mangle]
pub extern "C" fn ck_free_frame(frame: *mut CkFrame) {
    if frame.is_null() {
        return;
    }

    unsafe {
        let f = &mut *frame;
        if !f.data.is_null() {
            drop(Vec::from_raw_parts(f.data, f.len, f.len));
            f.data = std::ptr::null_mut();
            f.len = 0;
        }
    }
}

fn capture(x: f64, y: f64, width: f64, height: f64, show_cursor: bool) -> Result<CkFrame, i32> {
    if !scap::is_supported() {
        return Err(CK_NOT_SUPPORTED);
    }

    if !scap::has_permission() {
        return Err(CK_NO_PERMISSION);
    }

    let crop_area = if width > 0.0 && height > 0.0 {
        Some(Area { origin: Point { x, y }, size: Size { width, height } })
    } else {
        None
    };

    let options = Options {
        fps: 60,
        show_cursor,
        show_highlight: false,
        target: None,
        crop_area,
        output_type: FrameType::BGRAFrame,
        ..Default::default()
    };

    let mut capturer = Capturer::build(options).map_err(|_| CK_NO_PERMISSION)?;
    capturer.start_capture();
    let frame = capturer.get_next_frame();
    capturer.stop_capture();

    match frame {
        Ok(Frame::BGRA(f)) => {
            let mut data = f.data.into_boxed_slice().into_vec();
            data.shrink_to_fit();
            let len = data.len();
            let ptr = data.as_mut_ptr();
            std::mem::forget(data);
            Ok(CkFrame { data: ptr, len, width: f.width, height: f.height, stride: f.width * 4 })
        }
        Ok(_) => Err(CK_UNEXPECTED_FORMAT),
        Err(_) => Err(CK_NO_FRAME),
    }
}
