# Software Design Document (SDD)
**Project**: HumTrack  
**Date**: 2026-03-09  
**Version**: 0.1 (Draft)  

## 1. Introduction
### 1.1 Purpose
This document describes the architecture and high-level design of HumTrack.

## 2. System Architecture
### 2.1 Technology Stack
- **UI**: AvaloniaUI 11 (C#, XAML)
- **Core Logic**: .NET 8, Zero UI dependencies
- **Computer Vision**: EmguCV 4.9+
- **Rendering**: SkiaSharp for hardware-accelerated 2D overlays

### 2.2 Component Diagram
[To be populated during Phase 1]

## 3. Module Design
### 3.1 Tracking Strategy Pattern
The `ITrackingEngine` interface defines the contract for all tracking algorithms (Template Matching, KCF, CSRT, Optical Flow, Blob Detection).

### 3.2 Biomechanics Models
The `HumTrack.Biomechanics` library contains 3D-ready spatial definitions (`Point3D`), `RigidSegment` definitions, and C3D data export structures.
