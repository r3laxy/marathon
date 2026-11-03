@ui @group-preorder
Feature: Group pre-order on screen

  The owner of a reservation follows the group pre-order in the storefront: who joined, what
  each person chose and what it comes to, removes a guest and sends the pre-order to the
  restaurant, where the waiter sees it. The guests' side is prepared through the service,
  because guests join from their own devices.

  Background:
    Given the restaurant has a waiter account
    And a restaurant with 2 dishes on the menu
    And a customer named Olivia who has booked a table for 4 guests

  Rule: The owner sees the group on screen

    @smoke @level1
    Scenario: The owner sees every guest with their dishes and totals
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Mia has put 1 portion of dish 2 in their cart
      And the guest Noah has put 1 portion of dish 2 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has submitted their cart
      When Olivia signs in and opens the group pre-order
      Then the screen lists 3 participants
      And the screen shows for Mia:
        | dish | portions |
        | 1    | 2        |
        | 2    | 1        |
      And the screen shows for Noah:
        | dish | portions |
        | 2    | 1        |
      And the screen shows the grand total of:
        | dish | portions |
        | 1    | 2        |
        | 2    | 2        |

    @level1 @negative
    Scenario: Guests who have not confirmed are marked as still choosing
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has submitted their cart
      When Olivia signs in and opens the group pre-order
      Then the screen shows Mia as "Submitted"
      And the screen shows Noah as "Still choosing"
      And the screen shows no dishes for Noah

    @level1 @negative
    Scenario: A group nobody has joined shows only the owner
      When Olivia signs in and opens the group pre-order
      Then the screen lists 1 participant
      And the screen shows no dishes for the owner
      And the screen shows that nobody has added dishes yet

    @level1 @negative
    Scenario: Another customer does not see someone else's group pre-order
      Given another customer named Peter is signed in
      When Peter signs in and opens the group pre-order
      Then the group pre-order is not shown

  Rule: The owner manages the group on screen

    @level3
    Scenario: Removing a guest takes them and their dishes off the screen
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Noah has put 1 portion of dish 2 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has submitted their cart
      When Olivia signs in and opens the group pre-order
      And Olivia removes the guest Mia on the screen
      Then the screen lists 2 participants
      And the screen does not list the guest Mia
      And the screen shows the grand total of:
        | dish | portions |
        | 2    | 1        |

    @level2
    Scenario: Submitting sends the confirmed carts to the restaurant
      Given Olivia has placed their own pre-order of dish 1
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has joined the group pre-order
      And the guest Noah has put 1 portion of dish 2 in their cart
      And the guest Noah has submitted their cart
      When Olivia signs in and opens the group pre-order
      And Olivia submits the group pre-order on the screen
      Then the screen shows the pre-order as sent
      And the consolidated pre-order lists:
        | dish | portions |
        | 1    | 3        |
        | 2    | 1        |

  Rule: The waiter sees what the owner sent

    @e2e @level3
    Scenario: Guests choose, the owner removes one and submits, and the waiter sees the rest
      Given Olivia has placed their own pre-order of dish 2
      And the guest Mia has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Mia has submitted their cart
      And the guest Noah has joined the group pre-order
      And the guest Noah has put 1 portion of dish 2 in their cart
      And the guest Noah has submitted their cart
      When Olivia signs in and opens the group pre-order
      And Olivia removes the guest Mia on the screen
      And Olivia submits the group pre-order on the screen
      And Olivia signs out
      And the waiter signs in and opens the pre-order of Olivia
      Then the waiter sees on the pre-order page:
        | dish | portions |
        | 2    | 2        |
      And the waiter does not see dish 1 on the pre-order page
