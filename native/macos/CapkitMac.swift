// Capkit - A program that allows you to take screenshots and share any file type
// Copyright (c) 2007-2026 ShareX Team
// Licensed under the GNU General Public License v3 or later.
//
// macOS bridge for the .NET app: a C ABI around ScreenCaptureKit, loaded as libcapkit_mac.dylib.
// Rectangles are in points (Quartz global coordinates, origin at the top-left of the main display).

import CoreGraphics
import Foundation
import ScreenCaptureKit

private let ckOK: Int32 = 0
private let ckUnsupportedOS: Int32 = 1
private let ckNoPermission: Int32 = 2
private let ckTimeout: Int32 = 3
private let ckFailed: Int32 = 4
private let ckNoDisplay: Int32 = 5

private enum CaptureError: Error {
    case noDisplay
    case emptyRegion
}

/// Holds the async result for the synchronous C entry point.
private final class ResultBox: @unchecked Sendable {
    private let lock = NSLock()
    private var value: Result<CGImage, Error>?

    func set(_ result: Result<CGImage, Error>) {
        lock.lock()
        value = result
        lock.unlock()
    }

    func get() -> Result<CGImage, Error>? {
        lock.lock()
        defer { lock.unlock() }
        return value
    }
}

@available(macOS 14.0, *)
private func captureImage(rect: CGRect, scale: CGFloat, showCursor: Bool) async throws -> CGImage {
    let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)

    // Version 1 captures from one display: the one holding most of the region.
    let display = content.displays.max { a, b in
        area(CGDisplayBounds(a.displayID).intersection(rect)) < area(CGDisplayBounds(b.displayID).intersection(rect))
    }

    guard let display else { throw CaptureError.noDisplay }

    let displayBounds = CGDisplayBounds(display.displayID)
    let region = rect.intersection(displayBounds)
    guard !region.isNull, region.width >= 1, region.height >= 1 else { throw CaptureError.emptyRegion }

    let filter = SCContentFilter(display: display, excludingWindows: [])
    let configuration = SCStreamConfiguration()
    configuration.sourceRect = region.offsetBy(dx: -displayBounds.minX, dy: -displayBounds.minY)
    configuration.width = Int((region.width * scale).rounded())
    configuration.height = Int((region.height * scale).rounded())
    configuration.showsCursor = showCursor
    configuration.pixelFormat = kCVPixelFormatType_32BGRA
    configuration.captureResolution = .best

    return try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration)
}

private func area(_ rect: CGRect) -> CGFloat {
    rect.isNull ? 0 : rect.width * rect.height
}

/// Copies the image into a malloc'd BGRA buffer (premultiplied, little-endian ARGB = B,G,R,A in memory).
private func copyPixels(_ image: CGImage, data: UnsafeMutablePointer<UnsafeMutableRawPointer?>,
                        width: UnsafeMutablePointer<Int32>, height: UnsafeMutablePointer<Int32>,
                        stride: UnsafeMutablePointer<Int32>) -> Int32 {
    let w = image.width
    let h = image.height
    let bytesPerRow = w * 4

    guard let buffer = malloc(bytesPerRow * h) else { return ckFailed }

    let bitmapInfo = CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue
    guard let context = CGContext(data: buffer, width: w, height: h, bitsPerComponent: 8, bytesPerRow: bytesPerRow,
                                  space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: bitmapInfo) else {
        free(buffer)
        return ckFailed
    }

    context.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    data.pointee = buffer
    width.pointee = Int32(w)
    height.pointee = Int32(h)
    stride.pointee = Int32(bytesPerRow)
    return ckOK
}

/// Captures a region. Call from a background thread: it blocks until ScreenCaptureKit answers (10 s at most).
@_cdecl("ck_mac_capture")
public func ck_mac_capture(_ x: Double, _ y: Double, _ w: Double, _ h: Double, _ scale: Double, _ showCursor: Bool,
                           _ data: UnsafeMutablePointer<UnsafeMutableRawPointer?>,
                           _ width: UnsafeMutablePointer<Int32>, _ height: UnsafeMutablePointer<Int32>,
                           _ stride: UnsafeMutablePointer<Int32>) -> Int32 {
    data.pointee = nil

    guard #available(macOS 14.0, *) else { return ckUnsupportedOS }
    guard CGPreflightScreenCaptureAccess() else { return ckNoPermission }

    let box = ResultBox()
    let done = DispatchSemaphore(value: 0)
    let rect = CGRect(x: x, y: y, width: w, height: h)

    Task.detached {
        do {
            box.set(.success(try await captureImage(rect: rect, scale: CGFloat(scale), showCursor: showCursor)))
        } catch {
            box.set(.failure(error))
        }
        done.signal()
    }

    if done.wait(timeout: .now() + 10) == .timedOut {
        return ckTimeout
    }

    switch box.get() {
    case .success(let image):
        return copyPixels(image, data: data, width: width, height: height, stride: stride)
    case .failure(CaptureError.noDisplay):
        return ckNoDisplay
    default:
        return ckFailed
    }
}

@_cdecl("ck_mac_free")
public func ck_mac_free(_ data: UnsafeMutableRawPointer?) {
    free(data)
}
