@US05
Feature: Manual correction of extracted data
  As a MYPE business owner
  I want to correct the data extracted from my invoice
  So that the legal and financial information is exact before continuing

  Background:
    Given a MYPE business owner is signed in through the API Gateway
    And the business owner's company KYC is "VERIFIED"
    And the business owner has a readable PDF invoice
    And the OCR reads the invoice with a low-confidence due date
    When the business owner uploads the invoice
    And the system finishes the extraction and initial validation
    Then the invoice status is "REQUIRES_REVIEW"

  Scenario: A coherent correction updates the invoice and lets it continue
    When the business owner confirms the invoice data with a total of 1650.00
    Then the invoice status is "CONSISTENCY_PASSED"
    And the invoice reports the issue "MYPE_CONFIRMED_DATA"
    And the invoice keeps the confirmed total of 1650.00
    And the invoice is ready to be sent to the auction

  Scenario: A correction whose amounts do not add up stays in human review
    When the business owner confirms the invoice data with a total of 1800.00
    Then the invoice status is "REQUIRES_REVIEW"
    And the invoice reports the issue "TOTALS_DO_NOT_RECONCILE"
    And the invoice is not sent to the auction

  # Pendiente: el servicio no compara el total corregido con el que leyó el OCR,
  # así que todavía no puede detectar una diferencia significativa entre ambos.
  @pending @ignore
  Scenario: A corrected total far from the extracted one goes to human review
    When the business owner confirms the invoice data with a total of 9999.00
    Then the invoice status is "REQUIRES_REVIEW"
    And the invoice is not sent to the auction
