# India/CDSCO Regulatory Baseline

**Document ID:** HC-REG-BASE-IND-001  
**Status:** Preliminary applicability baseline; qualified review required  
**Reviewed:** 2026-09-06

## Primary jurisdiction

India. The baseline must be refreshed from current official sources before affected implementation and every medically positioned release.

Primary source entry points:

- CDSCO Medical Devices: https://cdsco.gov.in/opencms/opencms/en/Medical-Device-Diagnostics/Medical-Device-Diagnostics/
- CDSCO Acts and Rules: https://cdsco.gov.in/opencms/opencms/en/Acts-Rules/
- MeitY data-protection framework: https://www.meity.gov.in/data-protection-framework
- BIS standards portal: https://www.bis.gov.in/
- CDSCO Guidance Document on Medical Device Software under MDR 2017,
  CDSCO/MD/GD/MDSW/01/2026, final dated 2026-07-21:
  https://www.cdsco.gov.in/opencms/export/sites/CDSCO_WEB/Pdf-documents/Guidance-document-on-Medical-Device-Software-under-MDR-2017.pdf
- Digital Personal Data Protection Rules, 2025, notified 2025-11-13 with
  staged commencement:
  https://www.meity.gov.in/static/uploads/2025/11/53450e6e5dc0bfa85ebd78686cadad39.pdf

## Applicable framework to assess

- Medical Devices Rules, 2017, as currently amended/notified.
- Current CDSCO medical-device software guidance and classification notices.
- Applicable Indian/BIS-adopted standards.
- Digital Personal Data Protection framework and current commencement/rules.
- Applicable institutional ethics, consent, research, retention, and incident policies.

This list is a review scope, not a legal conclusion that every item applies.

## Required classification assessment

Assess separately:

1. HumCapture acquisition software.
2. Existing HumTrack analytics software.
3. HumCapture and HumTrack as a marketed or deployed combined system.

Document intended purpose, user, population, environment, information produced, downstream decisions, claims, accessory/system relationship, and significance to healthcare decisions.

## Current project position

- HumCapture is described as medical-adjacent acquisition software for trained supervised operators.
- It produces verified video/timing/IMU packages and acquisition-conformance information.
- It does not currently claim diagnosis, treatment recommendation, clinical accuracy, hardware synchronization, CDSCO approval, certification, or a medical-device class.
- Final applicability, class, licensing path, QMS obligations, evidence, and labeling require qualified Indian regulatory review.
- The 2026 CDSCO guidance illustrates that software solely performing transfer,
  storage, archive, format, or communication may fall outside MDSW when it has
  no additional medical purpose or system impact. HumCapture also performs
  acquisition, timing, verification, and conformance functions, so that
  illustration is not used here as a classification conclusion.
- I0.3A/I0.3B add engineering contracts for integrity, traceability, recoverable
  connectivity loss, attended peer enrollment, mutual TLS, authorization,
  credential lifecycle, and redacted audit. Runtime security implementation,
  hostile-network/penetration evidence, encryption at rest beyond credential
  protection, retention, consent/notice, incident controls, controlled standards
  editions, and qualified review remain open; no conformity claim follows.

## Implementation gate

Before work that affects intended use, claims, subject data, timing/quality, security, export, or release:

- Refresh official sources and record dates/links.
- Identify applicable controlled requirements.
- Link risks, controls, interfaces, and verification.
- Obtain Architect and Regulatory/Risk review.
- Escalate legal interpretation or classification to a qualified professional.

## Release gate

No medically positioned release is authorized until intended use, applicability/classification, standards matrix, risk management, privacy, usability, security, verification, claims, and qualified approvals are complete and recorded.
