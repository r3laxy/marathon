@api @group-preorder @level3
Feature: Group pre-order participants

  The owner of a group pre-order can remove a guest, for instance somebody who joined by
  mistake or ordered too much. What the guest chose no longer counts and they can no longer
  use their cart. The owner themselves stays in the group.

  Background:
    Given the restaurant has a waiter account
    And a restaurant with 2 dishes on the menu
    And a customer named Olivia who has booked a table for 4 guests
    And the guest Mia has joined the group pre-order
    And the guest Noah has joined the group pre-order
    And the guest Mia has put 2 portions of dish 1 in their cart
    And the guest Noah has put 1 portion of dish 2 in their cart

  Rule: Removing a guest takes them and their dishes out of the group

    @smoke
    Scenario: The owner removes one of several guests
      When Olivia removes the guest Mia from the group pre-order
      Then it works
      And the group pre-order has 2 participants
      And the guest Mia is not listed
      And the guest Noah is listed

    Scenario: What the removed guest chose no longer counts towards the total
      When Olivia removes the guest Mia from the group pre-order
      Then the group total is the price of:
        | dish | portions |
        | 2    | 1        |

    Scenario: A guest who already confirmed their cart can still be removed
      Given the guest Mia has submitted their cart
      When Olivia removes the guest Mia from the group pre-order
      Then the guest Mia is not listed

    Scenario: Removing the last guest leaves only the owner
      Given Olivia has removed the guest Noah from the group pre-order
      When Olivia removes the guest Mia from the group pre-order
      Then the group pre-order has 1 participant
      And the owner is listed first
      And the group total is zero

    Scenario: A removed guest can no longer use their cart
      Given Olivia has removed the guest Mia from the group pre-order
      When the guest Mia puts 1 portion of dish 1 in their cart
      Then the guest is told that their session is not valid

    Scenario: A removed guest makes room for someone else
      Given Olivia has removed the guest Mia from the group pre-order
      When anyone checks how many people have joined
      Then the group status shows 2 of 4 participants

  Rule: Some removals are not possible

    @negative
    Scenario: The owner cannot remove themselves
      When Olivia removes themselves from the group pre-order
      Then the request is rejected saying "The owner session cannot be removed from the group."

    @negative
    Scenario: A guest who is not in the group cannot be removed
      When Olivia removes a guest who is not in the group pre-order
      Then they are told it could not be found

    @negative
    Scenario: A guest of another group cannot be removed
      Given another customer named Peter who has booked a table for 4 guests
      And the guest Zoe has joined the group pre-order of Peter
      When Olivia tries to remove Zoe, a guest of another group, from the group pre-order
      Then the request is rejected saying "Guest session does not belong to this group reservation."

    @negative
    Scenario: Nobody can be removed from a group pre-order that does not exist
      When Olivia removes the guest Mia from a group pre-order that does not exist
      Then they are told it could not be found

  Rule: Only the owner of the reservation removes guests

    @negative
    Scenario: Another customer cannot remove a guest
      Given another customer named Peter is signed in
      When Peter tries to remove the guest Mia from the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: A waiter cannot remove a guest
      When the waiter tries to remove the guest Mia from the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: Someone who is not signed in cannot remove a guest
      When someone who is not signed in tries to remove the guest Mia from the group pre-order
      Then they are turned away because they are not signed in

  Rule: A whole group pre-order works from the first guest to the finished order

    @smoke @e2e
    Scenario: Guests join, one is removed and the rest end up in the order the waiter sees
      Given Olivia has placed their own pre-order of dish 2
      And the guest Noah has submitted their cart
      And the guest Mia has submitted their cart
      And Olivia has removed the guest Mia from the group pre-order
      When Olivia submits the group pre-order
      Then the submitted dishes are:
        | dish | portions |
        | 2    | 1        |
      When Olivia looks at their order
      Then the order holds:
        | dish | portions |
        | 2    | 2        |
      When the waiter looks at the reservation
      Then the waiter sees these dishes in the pre-order:
        | dish | portions |
        | 2    | 2        |
      And the waiter does not see dish 1 in the pre-order
